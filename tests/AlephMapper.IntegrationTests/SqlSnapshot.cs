namespace AlephMapper.IntegrationTests;

internal static class SqlSnapshot
{
    private static readonly IEqualityComparer<string> LineEndingComparer = new LineEndingInsensitiveComparer();

    public static async Task AssertMatches(string name, string actual)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "SqlSnapshots", name + ".sql");
        var expected = await File.ReadAllTextAsync(path);
        await Assert.That(actual).IsEqualTo(expected, LineEndingComparer);
    }

    private sealed class LineEndingInsensitiveComparer : IEqualityComparer<string>
    {
        public bool Equals(string? x, string? y)
        {
            if (x is null || y is null) return x == y;
            var xIndex = 0;
            var yIndex = 0;
            while (true)
            {
                var left = NextCharacter(x, ref xIndex);
                var right = NextCharacter(y, ref yIndex);
                if (left != right) return false;
                if (left < 0) return true;
            }
        }

        public int GetHashCode(string value)
        {
            var hash = new HashCode();
            var index = 0;
            int character;
            while ((character = NextCharacter(value, ref index)) >= 0)
                hash.Add(character);
            return hash.ToHashCode();
        }

        private static int NextCharacter(string value, ref int index)
        {
            if (index == value.Length) return -1;
            var character = value[index++];
            if (character != '\r') return character;
            if (index < value.Length && value[index] == '\n') index++;
            return '\n';
        }
    }
}
