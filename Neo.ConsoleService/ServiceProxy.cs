// Copyright (C) 2016-2021 The Neo Project.
// 
// The Neo.ConsoleService is free software distributed under the MIT 
// software license, see the accompanying file LICENSE in the main directory
// of the project or http://www.opensource.org/licenses/mit-license.php 
// for more details.
// 
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

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
