using System.ServiceProcess;

namespace Neo.ConsoleService
{
    internal class ServiceProxy : ServiceBase
    {
        private readonly ConsoleServiceBase service;

        public ServiceProxy(ConsoleServiceBase service)
        {
            this.service = service;
        }

        protected override void OnStart(string[] args)
        {
            service.OnStart(args);
        }

        protected override void OnStop()
        {
            service.OnStop();
        }
    }
}
