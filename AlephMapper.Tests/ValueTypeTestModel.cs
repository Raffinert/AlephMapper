namespace AlephMapper.Tests;

internal struct ValueTypeSource
{
    public int Age { get; set; }
    public bool IsActive { get; set; }
    public DateTime BirthDate { get; set; }
    public decimal Salary { get; set; }
    public double Height { get; set; }
    public string Name { get; set; }
}

internal struct ValueTypeDestination
{
    public int Age { get; set; }
    public bool IsActive { get; set; }
    public DateTime BirthDate { get; set; }
    public decimal Salary { get; set; }
    public double Height { get; set; }
    public string Name { get; set; }
}

internal static partial class ValueTypeMapper
{
    [Updateable]
    public static ValueTypeDestination MapToDestination(ValueTypeSource source)
        => new ValueTypeDestination
        {
            Age = source.Age,
            IsActive = source.IsActive,
            BirthDate = source.BirthDate,
            Salary = source.Salary,
            Height = source.Height,
            Name = source.Name
        };
}