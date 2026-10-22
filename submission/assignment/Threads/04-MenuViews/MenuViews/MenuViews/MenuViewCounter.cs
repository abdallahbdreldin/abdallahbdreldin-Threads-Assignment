namespace MenuViews;

public sealed class MenuViewCounter
{
    public int Views;

    public async Task RegisterView()
    {
        Interlocked.Increment(ref Views);
    }
}
