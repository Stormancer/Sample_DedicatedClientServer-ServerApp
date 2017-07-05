using Stormancer.Server.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server.Plugins.Configuration
{
    public interface IConfiguration
    {
        dynamic Settings { get; }

        event EventHandler<dynamic> SettingsChanged;
    }

    internal class EnvironmentConfiguration : IConfiguration, IDisposable
    {
        private readonly IEnvironment _env;
        public EnvironmentConfiguration(IEnvironment environment)
        {
            _env = environment;
            environment.ConfigurationChanged += RaiseSettingsChanged;
            Settings = environment.Configuration;
        }

        private void RaiseSettingsChanged(object sender,dynamic args)
        {
            var handler = SettingsChanged;
            Settings = _env.Configuration;
            if (handler != null)
            {
                handler(this, this.Settings);
            }
        }

        public dynamic Settings
        {
            get;
            private set;
        }

        public event EventHandler<dynamic> SettingsChanged;

        public void Dispose()
        {
            _env.ConfigurationChanged -= RaiseSettingsChanged;
        }
    }
}
