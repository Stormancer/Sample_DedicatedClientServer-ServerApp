using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Configuration
{
    public interface IConfigurationRefresh
    {
        void Init(dynamic config);
        void ConfigChanged(dynamic newConfig);
    }
}
