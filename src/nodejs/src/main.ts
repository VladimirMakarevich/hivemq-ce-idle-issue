import mqtt, { IClientOptions, MqttClient } from 'mqtt';

// Configuration Constants
const SUBSCRIPTION_COUNT = 5;  // Number of shared subscriptions per client
const MAX_CLIENTS = 1;           // Number of MQTT clients to simulate
const BROKER_HOST = 'localhost'; // Replace with your MQTT broker host
const BROKER_PORT = 1883;        // Replace with your MQTT broker port
const USERNAME = 'admin';        // Replace with your MQTT username if required
const PASSWORD = 'hivemq';       // Replace with your MQTT password if required
// const USERNAME = '';        // Replace with your MQTT username if required
// const PASSWORD = '';       // Replace with your MQTT password if required

interface ClientInfo {
    clientId: number;
    client: MqttClient;
    confirmingClient: MqttClient;
    topics: string[];
}

const clientInfoArr: ClientInfo[] = [];
let shuttingDown = false;

// Generate shared subscription topics
function generateSharedTopics(): string[] {
    const topics: string[] = [];
    for (let i = 1; i <= SUBSCRIPTION_COUNT; i++) {
        const x = i.toString().padStart(4, '0');
        topics.push(`$share/overloadtest/overload/ce/${x}`);
    }
    return topics;
}

// Handler for incoming messages
async function handleMessage(clientId: number, topic: string, message: Buffer, clientConfirming: MqttClient): Promise<void> {
    const payload = message.toString();
    return new Promise((resolve, reject) => {
        clientConfirming.publish('processed/ce', payload, (err) => {
            if (err) {
                console.error(`${new Date().toISOString()} [ERROR] Client ${clientId} failed to publish processed message: ${err}`);
                reject(err);
            } else {
                resolve();
            }
        });
    });
}

// Connect and subscribe a single client
async function connectAndSubscribe(clientId: number, topics: string[], startTime: Date): Promise<{
    client: MqttClient;
    confirmingClient: MqttClient;
    topics: string[]
}> {
    const clientOptions: IClientOptions = {
        host: BROKER_HOST,
        port: BROKER_PORT,
        username: USERNAME,
        password: PASSWORD,
        protocolVersion: 5, // MQTT v5
        clientId: `JsSubscriber${clientId}`,
        clean: false,
    };

    console.log(`${new Date().toISOString()} [INFO] Connecting client ${clientId}...`);
    const client = mqtt.connect(clientOptions);

    await new Promise<void>((resolve, reject) => {
        client.on('error', (err) => {
            console.error(`${new Date().toISOString()} [ERROR] Client ${clientId} encountered MQTT error: ${err}`);
            reject(err);
        });
        client.on('connect', () => {
            console.log(`${new Date().toISOString()} [INFO] Client ${clientId} connected at ${new Date().toISOString()}`);
            resolve();
        });
    });

    // Subscribe to all shared topics
    for (const topic of topics) {
        await new Promise<void>((resolve, reject) => {
            client.subscribe(topic, { qos: 1 }, (err) => {
                const elapsed = new Date().getTime() - startTime.getTime();
                if (err) {
                    console.error(`${new Date().toISOString()} [ERROR] Client ${clientId} subscription error: ${err}`);
                    return reject(err);
                }
                console.warn(`${new Date().toISOString()} [WARN] Client ${clientId} subscribed to ${topic} | Elapsed: ${elapsed} ms`);
                resolve();
            });
        });
    }

    const totalElapsed = new Date().getTime() - startTime.getTime();
    console.warn(`${new Date().toISOString()} [WARN] Client ${clientId} completed subscriptions. Total Elapsed: ${totalElapsed} ms, Time: ${new Date().toISOString()}`);

    // Connect confirming client
    const confirmingOptions: IClientOptions = {
        host: BROKER_HOST,
        port: BROKER_PORT,
        username: USERNAME,
        password: PASSWORD,
        clientId: 'jsconfirming' // identifier for the confirming client
    };
    const clientConfirming = mqtt.connect(confirmingOptions);

    await new Promise<void>((resolve, reject) => {
        clientConfirming.on('error', (err) => {
            console.error(`${new Date().toISOString()} [ERROR] Confirming client encountered MQTT error: ${err}`);
            reject(err);
        });
        clientConfirming.on('connect', () => {
            console.log(`${new Date().toISOString()} [INFO] Confirming client connected`);
            resolve();
        });
    });

    // Listen for incoming messages on the main client
    client.on('message', async (topic, message) => {
        try {
            console.info(`${new Date().toISOString()} [INFO] Topic ${topic} receive the message: ${message}`);
            // await handleMessage(clientId, topic, message, clientConfirming);
        } catch (e) {
            console.error(`${new Date().toISOString()} [ERROR] Client ${clientId} encountered unexpected error: ${e}`);
        }
    });

    return { client, confirmingClient: clientConfirming, topics };
}

// Cleanup function to unsubscribe from topics and close clients
async function cleanup() {
    if (shuttingDown) return;
    shuttingDown = true;
    console.log(`${new Date().toISOString()} [INFO] Cleaning up resources...`);

    // Unsubscribe from topics and end clients
    for (const info of clientInfoArr) {
        const { client, confirmingClient, topics, clientId } = info;

        // End the clients
        await new Promise<void>((resolve) => {
            client.end(false, {}, () => {
                console.log(`${new Date().toISOString()} [INFO] Client ${clientId} main connection ended.`);
                resolve();
            });
        });

        await new Promise<void>((resolve) => {
            confirmingClient.end(false, {}, () => {
                console.log(`${new Date().toISOString()} [INFO] Client ${clientId} confirming client connection ended.`);
                resolve();
            });
        });
    }

    console.log(`${new Date().toISOString()} [INFO] All resources cleaned up. Exiting now.`);
    process.exit(0);
}

// Main execution function
async function main(): Promise<void> {
    const startTime = new Date();
    const topics = generateSharedTopics();
    console.log(`${new Date().toISOString()} [INFO] Generated ${topics.length} shared subscription topics.`);

    const tasks: Promise<void>[] = [];
    for (let clientId = 1; clientId <= MAX_CLIENTS; clientId++) {
        tasks.push(
            (async () => {
                const {
                    client,
                    confirmingClient,
                    topics: subscribedTopics
                } = await connectAndSubscribe(clientId, topics, startTime);
                clientInfoArr.push({ clientId, client, confirmingClient, topics: subscribedTopics });
            })()
        );
    }

    try {
        await Promise.all(tasks);
        console.log(`${new Date().toISOString()} [INFO] Main finished ${topics.length} execution.`);
        // The clients remain running to receive messages. Press Ctrl+C or send SIGTERM to stop.
    } catch (e) {
        console.error(`${new Date().toISOString()} [ERROR] An error occurred during execution: ${e}`);
        await cleanup();
    }
}

// Handle process signals to gracefully shutdown
process.on('SIGINT', async () => {
    console.log(`${new Date().toISOString()} [INFO] Caught SIGINT signal`);
    await cleanup();
});

process.on('SIGTERM', async () => {
    console.log(`${new Date().toISOString()} [INFO] Caught SIGTERM signal`);
    await cleanup();
});

// Run the main function
main().catch(async err => {
    console.error(`${new Date().toISOString()} [ERROR] ${err}`);
    await cleanup();
});
