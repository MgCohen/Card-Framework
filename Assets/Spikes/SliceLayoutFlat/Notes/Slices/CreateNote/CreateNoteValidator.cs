namespace SpikeFlat.Notes.Slices.CreateNote
{
    internal static class CreateNoteValidator
    {
        public static bool IsValid(CreateNoteRequest request) => !string.IsNullOrWhiteSpace(request.Title);
    }
}
