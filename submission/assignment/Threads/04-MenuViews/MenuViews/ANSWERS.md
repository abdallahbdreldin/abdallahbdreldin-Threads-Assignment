# Assignment 04 — Answers

Student name:  Abdullah Mostafa Bdreldin
Group / ID: net/g-1

## Why Expected and Actual disagree

- because there are lost updates, as many threads are trying to update the same value at the same time, and they are overwriting each other's changes.

!(lost updates)[screenshots/notAtomic.png]

## What you changed

- using interlocking to protect the update, so that only one thread can update the value at a time applying atomic operations.

!(Atomic)[screenshots/atomic.png]

## When would you protect the update differently

- if there was a condition on the update , i could use a lock to protect the update, so that only one thread can update the value at a time.
