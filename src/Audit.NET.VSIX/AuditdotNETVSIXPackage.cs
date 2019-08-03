#region License
// Copyright (c) 2015-2019, Sonatype Inc.
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
//     * Redistributions of source code must retain the above copyright
//       notice, this list of conditions and the following disclaimer.
//     * Redistributions in binary form must reproduce the above copyright
//       notice, this list of conditions and the following disclaimer in the
//       documentation and/or other materials provided with the distribution.
//     * Neither the name of Sonatype, OSS Index, nor the
//       names of its contributors may be used to endorse or promote products
//       derived from this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL SONATYPE BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
#endregion

using System;
using System.ComponentModel.Composition;
using System.Runtime.InteropServices;
using System.Threading;

using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Events;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.ComponentModelHost;
using NuGet.VisualStudio;

using TTasks = System.Threading.Tasks;

namespace Audit.NET.VSIX
{
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the
    /// IVsPackage interface and uses the registration attributes defined in the framework to
    /// register itself and its components with the shell. These attributes tell the pkgdef creation
    /// utility what data to put into .pkgdef file.
    /// </para>
    /// <para>
    /// To get loaded into VS, the package must be referred by &lt;Asset Type="Microsoft.VisualStudio.VsPackage" ...&gt; in .vsixmanifest file.
    /// </para>
    /// </remarks>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(PackageGuidString)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideService((typeof(INuGetPackagesAuditService)), IsAsyncQueryable = true)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExistsAndFullyLoaded_string, PackageAutoLoadFlags.BackgroundLoad)]
    public sealed class AuditdotNETVSIXPackage : AsyncPackage
    {
        public AuditdotNETVSIXPackage()
        {
            
        }
        
        /// <summary>
        /// Audit.NET.VSIXPackage GUID string.
        /// </summary>
        public const string PackageGuidString = "d6329e33-9e01-4db6-bb76-7af303523a8a";

        public static AuditdotNETVSIXPackage Instance
        {
            get;
            private set;
        }

        #region Package Members
        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to monitor for initialization cancellation, which can occur when VS is shutting down.</param>
        /// <param name="progress">A provider for progress updates.</param>
        /// <returns>A task representing the async work of package initialization, or an already completed task if there is none. Do not return null from this method.</returns>
        protected override async TTasks.Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            // When initialized asynchronously, the current thread may be a background thread at this point.
            // Do any initialization that requires the UI thread after switching to the UI thread.
            Instance = new AuditdotNETVSIXPackage();
            this.AddService(typeof(INuGetPackagesAuditService), CreateNuGetPackagesAuditServiceAsync);
            var s = (await this.GetServiceAsync(typeof(SComponentModel))) ?? throw new InvalidOperationException(string.Format(Resources.Culture, Resources.General_MissingService, typeof(SComponentModel).FullName));
            if (s != null)
            {
                vsComponentModel = (IComponentModel) s;
                var vsPackageInstallerEvents = vsComponentModel.GetService<IVsPackageInstallerEvents>();
                var vsPackageInstallerProjectEvents = vsComponentModel.GetService<IVsPackageInstallerProjectEvents>();

            }

            await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            _uiCtx = SynchronizationContext.Current;
            _vsMonitorSelection = await GetAsync<IVsMonitorSelection>();
            /*
            x = await GetServiceAsync(typeof(IVsPackageInstallerEvents));
            if (x != null)
            {
                _vsPackageInstallerEvents = (IVsPackageInstallerEvents)x;
            }
            else
            {
                throw new InvalidOperationException(string.Format(Resources.Culture, Resources.General_MissingService, typeof(IVsMonitorSelection).FullName));
            }
            */
            /*
            x = await GetServiceAsync(typeof(IVsOutputWindowPane));
            if (x != null)
            {
                _vsOutput = (IVsOutputWindowPane)x;
            }
            else
            {
                throw new InvalidOperationException(string.Format(Resources.Culture, Resources.General_MissingService, typeof(IVsMonitorSelection).FullName));
            }
            */
            await auditPackagesCommand.InitializeAsync(this);

            bool isSolutionLoaded = await IsSolutionLoadedAsync();

            if (isSolutionLoaded)
            {
                HandleOpenSolution();
            }

            // Listen for subsequent solution events
            SolutionEvents.OnAfterOpenSolution += HandleOpenSolution;

        }
        #endregion

        public async TTasks.Task<object> CreateNuGetPackagesAuditServiceAsync(IAsyncServiceContainer container, CancellationToken cancellationToken, Type serviceType)
        {
            NuGetPackagesAuditService service = new NuGetPackagesAuditService(this);
            await service.InitializeAsync(cancellationToken);
            return service;
        }

        private async TTasks.Task<TService> GetAsync<TService>()
        {
            var x = await GetServiceAsync(typeof(TService));
            if (x != null)
            {
                return (TService) x;
            }
            else
            {
                throw new InvalidOperationException(string.Format(Resources.Culture, Resources.General_MissingService, typeof(TService).FullName));
            }
        }

        private async System.Threading.Tasks.Task<bool> IsSolutionLoadedAsync()
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync();
            var solService = await (GetServiceAsync(typeof(SVsSolution)) ?? throw new Exception()) as IVsSolution;
            
            ErrorHandler.ThrowOnFailure(solService.GetProperty((int)__VSPROPID.VSPROPID_IsSolutionOpen, out object value));

            return value is bool isSolOpen && isSolOpen;
        }

        private void HandleOpenSolution(object sender = null, EventArgs e = null)
        {
            // Handle the open solution and try to do as much work
            // on a background thread as possible
        }
        #region Fields

        private SynchronizationContext _uiCtx;
        private IComponentModel vsComponentModel;
        //private uint _solutionNotBuildingAndNotDebuggingContextCookie;
        private IVsOutputWindowPane _vsOutput;
        private IVsMonitorSelection _vsMonitorSelection;
        private IVsPackageInstallerEvents _vsPackageInstallerEvents;
        #endregion

        
    }
}
