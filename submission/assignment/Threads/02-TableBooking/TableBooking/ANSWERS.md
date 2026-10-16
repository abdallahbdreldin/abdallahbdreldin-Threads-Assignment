# Assignment 02 — Answers

Student name:  Abdullah Mostafa Bdreldin	
Group / ID: net/g-1

## What is wrong

- when many users tryig to book the same seat at the same time, it causes a race condition and leads to overbooking.

![Without Locking](screenshots/withoutlock.png)

## How you fixed it

- I fixed the race condition issue by implementing a locking mechanism on the seat booking process.
- This ensures that when one user is booking a seat, other users are prevented from accessing that seat until the booking process is complete.

![With Locking](screenshots/withlock.png)