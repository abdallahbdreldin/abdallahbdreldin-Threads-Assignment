## Json File Store Design

# Overview

    Json File Store is responsible for reading and writing JSON files that are used to save the applications collections. It brings together file access, JSON serialization and JSON deserialization, error handling and synchronization.
    
    Each collection is saved as a JSON file inside the Data folder. The store uses System Text Json for serialization.
    
  # API Design
    
    The class offers methods for regular application tasks and synchronous methods, for comparing synchronous and asynchronous processing.
    
# Methods

   1)  Read Async <T>() :

    Converts a JSON file in an asynchronous way. Provides an instance of T if the file is not found.

   2) Write Async <T>() :

    Converts data and saves it to a JSON file in a way.

   3) Update Async <T>() :

    Carries out a read modify write action while keeping a lock on the file.

   4) Read <T>() :

    Reads and converts a JSON file in a way.

   5)  Write <T>() :

    Converts. Saves data in a synchronous way.