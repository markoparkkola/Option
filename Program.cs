/*
 * Some example code. Didn't feel like writing unit tests.
 * I wrote a little extension for IEnumerable<T> so you can see
 * how Option type is much, much, much better than returning null
 * from FirstOrDefault. With Option type you cannot get yourself
 * into null-ref exceptions!
 */

var emptyList = new List<string>();
var filledList = new List<string> { "YEAH BUDDY!" };

var shouldBeEmptyOption = emptyList.FirstOrOption();
var shouldNotBeEmptyOption = filledList.FirstOrOption();

System.Diagnostics.Debug.Assert(shouldBeEmptyOption != Option.Some);
System.Diagnostics.Debug.Assert(shouldNotBeEmptyOption == Option.Some);

string s;
// should not throw
s = shouldNotBeEmptyOption.Value;
// throws for sure
s = shouldBeEmptyOption.Value;


public static class Ext
{
    public static Option<T> FirstOrOption<T>(this IEnumerable<T> enumerable)
    {
        return enumerable.Any() ? enumerable.First() : Option<T>.None;
    }
}

/// <summary>
/// Abstract base class just so that user can write variable == Option.Some.
/// </summary>
public abstract class Option
{
    class _Option : Option
    {
        public _Option(bool has)
            : base(has) { }
        public static readonly _Option _some = new _Option(true);
    }

    protected readonly bool _hasValue;

    protected Option(bool hasValue)
    {
        _hasValue = hasValue;
    }

    // These needs to be overloaded so that variable == Option.Some works.
    public static bool operator ==(Option a, Option b) => a._hasValue == b._hasValue;
    public static bool operator !=(Option a, Option b) => a._hasValue != b._hasValue;

    // Two overrides to make compiler happy.
    public override bool Equals(object? obj)
    {
        return obj is Option o ? o == this : false;
    }

    public override int GetHashCode()
    {
        return base.GetHashCode();
    }

    /// <summary>
    /// Return "some" option which is guaranteed to have a value, which can then be compared against
    /// other options to check if the other options has values.
    /// </summary>
    public static readonly Option Some = _Option._some;

    /// <summary>
    /// Helper method to check if option does not have a value.
    /// </summary>
    public static bool IsNone(Option option) => !option._hasValue;
    /// <summary>
    /// Helper method to check if option really has value.
    /// </summary>
    public static bool IsSome(Option option) => option._hasValue;
}

/// <summary>
/// Class to return optional values to avoid nullables and null-ref issues.
/// </summary>
/// <typeparam name="T">The type of your real variable.</typeparam>
public sealed class Option<T> : Option
{
    private static readonly Option<T> _none = new Option<T>();
    private readonly T _value = default!;

    public Option()
        : base(false)
    {
    }

    public Option(T value)
        : base(true)
    {
        _value = value;
    }

    /// <summary>
    /// Returns value, if this option has value. Throws otherwise.
    /// </summary>
    /// <exception cref="NullReferenceException">Option didn't have value.</exception>
    public T Value => _hasValue ? _value : throw new NullReferenceException();

    /// <summary>
    /// Daa.
    /// </summary>
    public bool HasValue => _hasValue;
    
    /// <summary>
    /// Not really necessary.
    /// </summary>
    public override string ToString() => _hasValue ? _value!.ToString()! : "N/A";

    /// <summary>
    /// Due to C# restrictions None must be defined here. Returns an empty option which does not have value.
    /// </summary>
    public static Option<T> None => _none;

    /// <summary>
    /// Implicit conversion to option type. This enables user to write for instance 
    /// <code>
    /// something ? 123 : Option<int>.None;
    /// </code>
    /// </summary>
    public static implicit operator Option<T>(T value) { return new Option<T>(value); }
}
