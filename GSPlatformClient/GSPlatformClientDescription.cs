using AMLCore.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GSPlatformClient
{
    public class GSPlatformClientDescription : IPluginDescription
    {
        public string InternalName => "GSPlatformClient";
        public string[] Authers => new[] { "acaly" };
        public string DisplayName => "通用联机平台客户端";
        public string Description => "";
        public int LoadPriority => 0;
        public PluginType PluginType => PluginType.EffectOnly;
        public string[] Dependencies => new string[0];
    }

    public class GSPlatformPreset : IPresetProvider
    {
        public PluginPreset[] GetPresetList()
        {
            return new[]
            {
                new PluginPreset
                {
                    Name = "通用联机平台客户端",
                    PluginLists = "GSPlatformClient",
                    Options = new List<Tuple<string, string>>(),
                },
            };
        }
    }
}
