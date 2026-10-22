# Assignment 03 — Answers

Student name:  Abdullah Mostafa Bdreldin
Group / ID: net/g-1

1. Provides synchronization mechanism for access to objects , allowing one thread at a time to access a critical section protected by the same lock.

2. it compiles into Monitor.Enter and Monitor.Exit .

3. 
	1. lock automatically releases the lock, even if an exception is thrown, ensuring that the lock is always released properly.
	monitor requires explicit calls to Monitor.Exit.

	2. monitor provides more flexibility, such as Wait and Pulse, while lock is limited to a simple mutual exclusion mechanism.

4. 
	1. Monitor.Wait releases the lock and waits for a notification
	2. Monitor.Pulse signals a waiting thread to wake up and reacquire the lock.
	3. Monitor.TryEnter attempts to acquire the lock without blocking, we can control the timeout and check if the lock was acquired successfully.

5. # Lock

		- private readonly object _lock = new();
				
	    public void Increment()
	    {
	        lock (_lock)
	        {
	            count++;
	        }
	    }

	# Monitor

		- private readonly object _lock = new();
				
		public void Increment()
		{
			Monitor.Enter(_lock);
			try
			{
				count++;
			}
			finally
			{
				Monitor.Exit(_lock);
			}
		}

Links:

- https://learn.microsoft.com/en-us/dotnet/api/system.threading.monitor?view=net-10.0
- https://medium.com/@kamlesh90/lock-vs-monitor-in-c-a16560c49a23
