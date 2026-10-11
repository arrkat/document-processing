namespace Integration.Tests.TestSupport;

public static class Eventually
{
    /// <summary>Polls <paramref name="condition"/> until it is true; fails the test after <paramref name="timeout"/> (default 10 s).</summary>
    public static async Task TrueAsync(Func<Task<bool>> condition, string because, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(10));
        while (!await condition())
        {
            if (DateTime.UtcNow > deadline)
                Assert.Fail($"Timed out waiting until {because}.");
            await Task.Delay(20);
        }
    }
}
