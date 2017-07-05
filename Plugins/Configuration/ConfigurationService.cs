using Stormancer.Server;
using Stormancer.Server.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Configuration
{
    public class ConfigurationService
    {
        private readonly Dictionary<IConfigurationRefresh, EventHandler<EventArgs>> _registrations = new Dictionary<IConfigurationRefresh, EventHandler<EventArgs>>();
        private readonly IEnvironment _environment;

        public ConfigurationService(IEnvironment environment)
        {
            _environment = environment;
        }

        public void RegisterComponent(IConfigurationRefresh component)
        {
            component.Init(_environment.Configuration);
            EventHandler<EventArgs> registration = (sender, _) => component.ConfigChanged(((IEnvironment)sender).Configuration);
            _environment.ConfigurationChanged += registration;
            _registrations.Add(component, registration);
        }

        public void UnRegisterComponenet(IConfigurationRefresh component)
        {
            EventHandler<EventArgs> registration;
            if (_registrations.TryGetValue(component, out registration))
            {
                _environment.ConfigurationChanged -= registration;
                _registrations.Remove(component);
            }
        }
    }
}
