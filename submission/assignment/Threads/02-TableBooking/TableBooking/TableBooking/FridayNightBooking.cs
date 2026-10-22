namespace TableBooking;

public sealed class FridayNightBooking
{
    private readonly object _lock = new object();
    public int TablesLeft { get; private set; } = 5;
    public int Booked { get; private set; }

    public async Task Book(string customerName)
    {
        lock (_lock)
        {

            if (TablesLeft <= 0)
                return;

            Task.Delay(20);

            TablesLeft--;
            Booked++;
            Console.WriteLine($"{customerName} booked a table. Left={TablesLeft}");
        }
    }
}
