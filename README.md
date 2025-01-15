## Expected behavior

Subscribers should consistently receive messages as long as the connection is active and the broker has messages on the subscribed topics, regardless of load conditions.

> Under scenarios involving high message throughput and frequent topic publishing/subscribing, we observe that subscriber clients stop receiving messages from the HiveMQ CE broker at some point. Notably, the connection remains established and there are no any errors and warnings in the logs, but message flow ceases. When the consumer client is restarted, messages are temporarily resumed, but after some time they stop again. This behavior is specifically observed when using MqttQualityOfServiceLevel = AtLeastOnce in combination with shared subscriptions.

## Actual behavior

After an initial period of normal message flow, subscribers become idle and do not receive any further messages until they are restarted.

## To Reproduce

### Steps

1. Set up a HiveMQ broker (either default or custom configuration).
- docker run -d --name hivemq_ce_latest_8g -p 8080:8080 -p 1883:1883 -e JAVA_OPTS="-Xmx8g" hivemq/hivemq-ce:latest
2. Launch 20 publisher clients with cleanStart = false and protocolVersion = 5, publishing messages to 5 shared topics at a high rate.
- [How to run the .NET Publishers and Consumer](https://github.com/VladimirMakarevich/hivemq-ce-idle-issue/tree/main/src/dotnet#readme)
3. Launch a single subscriber client (in .NET, JS, or Python) with cleanStart = false and protocolVersion = 5, subscribing to the same 5 shared topics using QoS=AtLeastOnce.
4. Observe that after some time the subscriber stops receiving messages (even though the connection remains active and there are no errors).

### Reproducer code

[How to run the .NET Publishers and Consumer](https://github.com/VladimirMakarevich/hivemq-ce-idle-issue/tree/main/src/dotnet#readme)

## Details

- Affected HiveMQ CE version(s): latest (and 2023.5)
- Used JVM version: -
- [The Link to the repository where you can find all code examples, conf, logs and traces, videos and screenshots.](https://github.com/VladimirMakarevich/hivemq-ce-idle-issue)

## What We Have Observed & Collected:

1. Reproduction Code & Setup:

- We have shared code samples and a repository link demonstrating how to reproduce this issue.
	- We have [three different consumer clients](https://github.com/VladimirMakarevich/hivemq-ce-idle-issue/tree/main/src) written on three different technologies, and the issue is reproducible in all cases.
		- NOTE: Our main stack is C# with .NET Core.
- We have a [screen-recording video](https://github.com/VladimirMakarevich/hivemq-ce-idle-issue/blob/main/hivemq_idle.mp4) illustrating the steps to trigger the behavior.

2. Broker Configurations:

- We have tested with both a default HiveMQ CE broker configuration and a [custom configuration](https://github.com/VladimirMakarevich/hivemq-ce-idle-issue/tree/main/custom%20broker%20conf). The issue occurs in both scenarios.

3. Logs & Diagnostics:

- We have broker [log files and diagnostic traces](https://github.com/VladimirMakarevich/hivemq-ce-idle-issue/tree/main/diagnostics%20and%20logs) from two separate test runs that exhibit the idle subscriber behavior.

4. Testing Environment & Components:

- **Publishers:**
	- Implemented in .NET
	- Connection settings: `cleanStart = false`, `protocolVersion = 5`
	- Number of client publishers: **20**
	- Topics: **5 shared subscriptions**

- **Subscribers:**
	- Implemented using .NET, JavaScript, and Python clients
	- Connection settings: `cleanStart = false`, `protocolVersion = 5`
	- Number of subscribers: **1**
	- Subscriptions: **5 shared subscriptions**

5. When Does It Occur?

- When publishing and reading messages at high rates.
- Under heavy load conditions, like during resource-constrained conditions on the broker’s hosting environment (e.g., VM memory and CPU nearing limits).

6. Additional Testing:

- This issue does not occur in HiveMQ Enterprise Broker.
- This issue does not occur with the default Mosquitto Broker.
