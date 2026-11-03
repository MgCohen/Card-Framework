namespace SpikeFlat.Notes.Slices.CreateNote
{
    public static class LeakInternalRequest
    {
        public static bool Check(CreateNoteRequest r) => CreateNoteValidator.IsValid(r);
    }
}
