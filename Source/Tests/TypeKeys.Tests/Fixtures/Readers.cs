namespace TypeKeys.Tests;

public interface INoteReader { }
public abstract class AbstractReader : INoteReader { }
public sealed class ByeReader : INoteReader { }
public sealed class GenReader : INoteReader { }
public sealed class HelloReader : INoteReader { }
public sealed class OpenReader<T> : INoteReader { }
