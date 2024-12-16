## How to Run the .NET Publisher and Consumer

1. **Clone the Repository**  
    Use the following command to clone the repository:
    
    ```bash
    git clone https://github.com/VladimirMakarevich/hivemq-ce-idle-issue.git
    ```
    
2. **Navigate to the .NET Source Directory**  
    
    ```bash
    cd hivemq-ce-idle-issue/src/dotnet/
    ```
    
3. **Run the Publisher**  
    Execute the following command to run the publisher project:
    
    ```bash
    dotnet run --project Publisher
    ```

4. **Run the Consume**  
    Execute the following command to run the consumer project:
    
    ```bash
    dotnet run --project RawConsumer
    ```
