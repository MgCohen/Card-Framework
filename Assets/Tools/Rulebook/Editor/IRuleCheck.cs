using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Tools.Rulebook
{
    public interface IRuleCheck
    {
        string Kind { get; }
        IEnumerable<Violation> Run(JObject parameters, CheckContext context);
    }
}
