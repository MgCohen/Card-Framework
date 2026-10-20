using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace Tools.Rulebook
{
    public static class RulebookLoader
    {
        public const string RelativePath = "Tools/Rulebook/rulebook.json";

        public static string DefaultPath => Path.Combine(ProjectPaths.Root, RelativePath);

        public static RulebookFile Load(string path = null)
            => JsonConvert.DeserializeObject<RulebookFile>(File.ReadAllText(path ?? DefaultPath));

        // The schema file is for editors and the app; this is the authority the gate enforces.
        public static List<string> Validate(RulebookFile book)
        {
            var errors = new List<string>();
            if (book.Version != 1) errors.Add($"version must be 1, got {book.Version}");

            var bookIds = new HashSet<string>();
            foreach (var b in book.Books)
            {
                if (string.IsNullOrWhiteSpace(b.Id)) errors.Add("a book has no id");
                else if (!bookIds.Add(b.Id)) errors.Add($"book id '{b.Id}' is used twice");
            }

            var ruleIds = new HashSet<string>();
            foreach (var r in book.Rules)
            {
                var name = r.Id ?? "(no id)";
                if (string.IsNullOrWhiteSpace(r.Id)) errors.Add("a rule has no id");
                else if (!ruleIds.Add(r.Id)) errors.Add($"rule id '{r.Id}' is used twice");

                if (r.Book == null || !bookIds.Contains(r.Book)) errors.Add($"{name}: book '{r.Book}' does not exist");
                foreach (var (field, value) in new[] { ("title", r.Title), ("rule", r.Statement), ("why", r.Why), ("fix", r.Fix) })
                    if (string.IsNullOrWhiteSpace(value)) errors.Add($"{name}: '{field}' is empty");

                if (r.Status != "enforced" && r.Status != "planned") errors.Add($"{name}: status '{r.Status}' is not enforced or planned");
                if (r.Check == null || !CheckRegistry.Kinds.Contains(r.Check.Kind)) errors.Add($"{name}: check kind '{r.Check?.Kind}' is unknown");

                foreach (var e in r.Exceptions.Where(e => string.IsNullOrWhiteSpace(e.Target) || string.IsNullOrWhiteSpace(e.Reason) || string.IsNullOrWhiteSpace(e.Until)))
                    errors.Add($"{name}: an exception needs target, reason and until ({e.Target})");
            }
            return errors;
        }
    }
}
