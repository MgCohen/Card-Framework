using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Tools.Rulebook
{
    public sealed class RulebookFile
    {
        [JsonProperty("version")] public int Version;
        [JsonProperty("books")]   public List<Book> Books = new();
        [JsonProperty("rules")]   public List<Rule> Rules = new();
    }

    public sealed class Book
    {
        [JsonProperty("id")]          public string Id;
        [JsonProperty("title")]       public string Title;
        [JsonProperty("description")] public string Description;
    }

    public sealed class Rule
    {
        [JsonProperty("id")]         public string Id;
        [JsonProperty("book")]       public string Book;
        [JsonProperty("title")]      public string Title;
        [JsonProperty("rule")]       public string Statement;
        [JsonProperty("why")]        public string Why;
        [JsonProperty("fix")]        public string Fix;
        [JsonProperty("status")]     public string Status;
        [JsonProperty("check")]      public Check Check;
        [JsonProperty("exceptions")] public List<Exemption> Exceptions = new();

        public bool IsPlanned => Status == "planned";
    }

    public sealed class Check
    {
        [JsonProperty("kind")]   public string Kind;
        [JsonProperty("params")] public JObject Params = new();
    }

    // A known violation the rule tolerates for now. "until" names the work that removes it.
    public sealed class Exemption
    {
        [JsonProperty("target")] public string Target;
        [JsonProperty("reason")] public string Reason;
        [JsonProperty("until")]  public string Until;
    }

    public sealed class Violation
    {
        [JsonProperty("target")]   public string Target;
        [JsonProperty("detail")]   public string Detail;
        [JsonProperty("location")] public string Location;

        public Violation(string target, string detail, string location = null)
        {
            Target   = target;
            Detail   = detail;
            Location = location;
        }

        public override string ToString()
            => Location == null ? $"{Target}: {Detail}" : $"{Target}: {Detail} ({Location})";
    }
}
