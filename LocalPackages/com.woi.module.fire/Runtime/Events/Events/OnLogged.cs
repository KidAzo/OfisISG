using System.Collections.Generic;
using FireExtinguisher.Core;

namespace Woi.Events
{
    public struct OnLogged
    {
        public List<FireClass> SelectedClasses;
        public string UserName;
        public string UserId;
        public string TargetScene;
    }
}
