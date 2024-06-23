/* 
 * This went weird pretty fast when I tried this with Entity Framework.
 * The problem here is, if you call like db.Foo.Where(x => x.OptionField == Option.Some)
 * EF reads every record from Foo table and checks the property in client side. I could
 * possibly write some extension that does it in database but I'm not sure how it can be
 * done nicely.
 * 
 * But maybe you would want to pick Option and Option<T> classes here. They are little bit
 * more sophisticated here.
 */

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Reflection;

var builder = new DbContextOptionsBuilder<MyContext>();
builder.UseInMemoryDatabase("myentities");
builder.EnableSensitiveDataLogging();
var db = new MyContext(builder.Options);
db.Database.EnsureCreated();

db.MyEntities.Add(new MyEntity
{
    Id = 1,
    OptionalName = "my name",
});
db.MyEntities.Add(new MyEntity
{
    Id = 2,
    OptionalName = Option<string>.None
});

db.SaveChanges();

// Id=2 is left out of "myEntities" because it does not have optional name
var myEntities = db.MyEntities.Where(x => x.OptionalName == Option.Some).ToList();

public static class Ext
{
    /// <summary>
    /// Register Option types for Entity Framework. Basically this works but wouldn't recommend using it.
    /// I have terrible problem with this because EF returns nulls to me even when I convert them to Option<t>.None.
    /// </summary>
    public static PropertyBuilder<TProperty> RegisterOptionType<TProperty>(this PropertyBuilder<TProperty> propertyBuilder)
    {
        var baseType = propertyBuilder.Metadata.ClrType.GenericTypeArguments[0];
        var optionType = typeof(Option<>).MakeGenericType(baseType);
        var converterType = typeof(OptionConverter<,>).MakeGenericType(optionType, baseType);
        propertyBuilder.Metadata.IsNullable = true;
        propertyBuilder.HasConversion((ValueConverter)Activator.CreateInstance(converterType)!);
        return propertyBuilder;
    }

    public static Option<T> FirstOrOption<T>(this IEnumerable<T> enumerable, Func<T, bool>? fn)
    {
        if (!enumerable.Any())
        {
            return Option<T>.None;
        }

        if (fn == null)
        {
            return enumerable.First();
        }

        return enumerable.FirstOrDefault(fn) ?? Option<T>.None;
    }
}


public class OptionConverter<TOption, TProperty> : ValueConverter<TOption, TProperty?>
{
    private static readonly PropertyInfo _hasValuePropertyInfo;
    private static readonly PropertyInfo _valuePropertyInfo;
    private static readonly Type _optionType;
    private static readonly object _none;

    public OptionConverter()
        : base((x) => ConvertToGenericType(x), (x) => ConvertToOption(x))
    {
    }

    static OptionConverter()
    {
        _optionType = typeof(Option<>).MakeGenericType(typeof(TProperty));
        _hasValuePropertyInfo = _optionType.GetProperty("HasValue") ?? throw new InvalidOperationException($"Type {_optionType.Name} is not a valid type.");
        _valuePropertyInfo = _optionType.GetProperty("Value") ?? throw new InvalidOperationException($"Type {_optionType.Name} is not a valid type.");
        _none = _optionType.GetProperty("None")?.GetValue(null) ?? throw new InvalidOperationException($"Type {_optionType.Name} is not a valid type.");
    }

    public static TProperty? ConvertToGenericType(TOption value)
    {
        var hasValue = (bool)_hasValuePropertyInfo.GetValue(value)!;
        if (!hasValue)
        {
            return default;
        }

        return (TProperty)_valuePropertyInfo.GetValue(value)!;
    }

    public static TOption ConvertToOption(TProperty? value)
    {
        return value != null
            ? (TOption)Activator.CreateInstance(_optionType, value)!
            : (TOption)_none;
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

        protected override int HashCode => 0;

        protected override bool Compare(Option other) => throw new NotImplementedException();
    }

    protected readonly bool _hasValue;

    protected Option(bool hasValue)
    {
        _hasValue = hasValue;
    }

    protected abstract bool Compare(Option other);
    protected abstract int HashCode { get; }

    // Following operators needs to have nullable parameters because of EF.
    // Otherwise all this would be so beautiful.

    public static bool operator ==(Option? a, Option? b)
    {
        // If we are just checking of a or b has value (variable == Option.Some)
        // we only need to check if there is a value.
        if (a is _Option)
        {
            return b?.HasValue ?? false;
        }

        if (b is _Option)
        {
            return a?.HasValue ?? false;
        }

        // Otherwise we need to check do the values match also.
        return a?.Equals(b) ?? false;
    }

    public static bool operator !=(Option? a, Option? b)
    {
        // see above ^^^
        if (a is _Option)
        {
            return b?.HasValue ?? false;
        }

        if (b is _Option)
        {
            return !a?.HasValue ?? false;
        }

        return !a?.Equals(b) ?? false;
    }

    public override bool Equals(object? obj) => obj is Option o ? Compare(o) : false;

    public override int GetHashCode() => HashCode;

    /// <summary>
    /// Return "some" option which is guaranteed to have a value, which can then be compared against
    /// other options to check if the other options has values.
    /// </summary>
    public static readonly Option Some = _Option._some;

    /// <summary>
    /// Daa.
    /// </summary>
    public bool HasValue => _hasValue;

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
    public static implicit operator Option<T>(T? value) { return value == null ? None : new Option<T>(value); }

    /// <summary>
    /// For easy comparison between Option<T> and T.
    /// </summary>
    public static bool operator ==(Option<T> a, T? b)
    {
        return a == (Option<T>)b;
    }

    /// <summary>
    /// For easy comparison between Option<T> and T.
    /// </summary>
    public static bool operator !=(Option<T> a, T? b)
    {
        return a != (Option<T>)b;
    }

    public override bool Equals(object? obj) => obj is Option<T> o ? Compare(o) : false;

    public override int GetHashCode() => HashCode;

    // I'm not very good with this hash code thing...
    protected override int HashCode => _value!.GetHashCode();

    // So in order to know are the two Options the same
    // we need to compare their values also. Option class
    // compares only cases likes "does this Option has value".
    protected override bool Compare(Option other)
    {
        if (HasValue != other.HasValue)
        {
            return false;
        }

        var otherValue = other as Option<T> ?? throw new ArgumentNullException(nameof(other));
        return _value!.Equals(otherValue._value);
    }
}

public class MyContext : DbContext
{
    public MyContext(DbContextOptions options) : base(options) { }

    public DbSet<MyEntity> MyEntities { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MyEntity>()
            .HasKey(x => x.Id);
        modelBuilder.Entity<MyEntity>()
            .Property(x => x.OptionalName)
            .RegisterOptionType();

        base.OnModelCreating(modelBuilder);
    }
}

public class MyEntity
{
    public int Id { get; set; }
    public Option<string> OptionalName { get; set; } // God I hate EF
}
