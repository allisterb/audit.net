using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using TTasks = System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;

using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Shell;

namespace Audit.NET.VSIX
{
    public class NuGetPackagesAuditService : SNuGetPackagesAuditService, INuGetPackagesAuditService
    {
        private IAsyncServiceProvider asyncServiceProvider;
        private TTasks.TaskScheduler _uiCtx = TTasks.TaskScheduler.FromCurrentSynchronizationContext();
       

        public NuGetPackagesAuditService(IAsyncServiceProvider provider)
        {
            // constructor should only be used for simple initialization
            // any usage of Visual Studio service, expensive background operations should happen in the
            // asynchronous InitializeAsync method for best performance
            asyncServiceProvider = provider;
        }

        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            await TTasks.TaskScheduler.Default;
            // do background operations that involve IO or other async methods

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            // query Visual Studio services on main thread unless they are documented as free threaded explicitly.
            // The reason for this is the final cast to service interface (such as IVsShell) may involve COM operations to add/release references.

            //IVsShell vsShell = this.asyncServiceProvider.GetServiceAsync(typeof(SVsShell)) as IVsShell;
            // use Visual Studio services to continue initialization
        }
    }
}
