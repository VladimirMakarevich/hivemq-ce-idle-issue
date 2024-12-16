import asyncio
import logging
import signal
from aiomqtt import Client, MqttError, ProtocolVersion
from datetime import datetime

# Configure logging
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s [%(levelname)s] %(message)s',
    handlers=[logging.StreamHandler()]
)
logger = logging.getLogger(__name__)

# Configuration Constants
SUBSCRIPTION_COUNT = 5  # Number of shared subscriptions per client
MAX_CLIENTS = 1  # Number of MQTT clients to simulate
BROKER_HOST = 'localhost'  # Replace with your MQTT broker host
BROKER_PORT = 1883  # Replace with your MQTT broker port
USERNAME = 'admin'  # Replace with your MQTT username if required
PASSWORD = 'hivemq'  # Replace with your MQTT password if required
# USERNAME = ''  # Replace with your MQTT username if required
# PASSWORD = ''  # Replace with your MQTT password if required


# Handler for incoming messages
async def handle_message(client_id, message, client):
    # await client.publish("processed/ce", message.payload.decode())
    topic = message.topic
    payload = message.payload.decode()
    logger.info(f"Client {client_id} received message on {topic}: {payload}")
    # Add additional processing logic here if needed


# Function to generate shared subscription topics
def generate_shared_topics():
    enums = [f"{x:04d}" for x in range(1, SUBSCRIPTION_COUNT + 1)]
    topics = [f"$share/overloadtest/overload/ce/{x}" for x in enums]
    return topics


# Shutdown handler
def handle_shutdown(shutdown_event_hs, loop_hs):
    logger.info("Shutdown signal received. Initiating graceful shutdown...")
    shutdown_event_hs.set()
    for task_hs in asyncio.all_tasks(loop_hs):
        task_hs.cancel()


# Function to connect and subscribe a single client
async def connect_and_subscribe(client_id, topics, start_time, shutdown_event_cas):
    try:
        async with Client(
                hostname=BROKER_HOST,
                port=BROKER_PORT,
                username=USERNAME,
                password=PASSWORD,
                identifier=f"PythonSubscriber{client_id}",
                protocol=ProtocolVersion.V5,
                clean_start=False
        ) as client:
            logger.info(f"Client {client_id} connected at {datetime.utcnow()}")

            # Subscribe to all shared topics
            for topic in topics:
                await client.subscribe(topic, 1)
                elapsed = datetime.utcnow() - start_time
                logger.warning(
                    f"Client {client_id} subscribed to {topic} | Elapsed: {elapsed}"
                )

            total_elapsed = datetime.utcnow() - start_time
            logger.warning(
                f"Client {client_id} completed subscriptions. Total Elapsed: {total_elapsed}, Time: {datetime.utcnow()}"
            )

            async for message in client.messages:
                await handle_message(client_id, message, client)

            # async with Client(
            #     hostname=BROKER_HOST,
            #     port=BROKER_PORT,
            #     username=USERNAME,
            #     password=PASSWORD,
            #     identifier="confirming"
            # ) as clientConfirming:
            #     async for message in client.messages:
            #         await handle_message(client_id, message, clientConfirming)

            # # Listen for incoming messages
            # async with client.unfiltered_messages() as messages:
            #     # await client.subscribe("#")  # Ensure all messages are captured
            #     async for message in messages:
            #         await handle_message(client_id, message)

            # Unsubscribe from all topics during shutdown
            if shutdown_event_cas.is_set():
                for topic in topics:
                    try:
                        await client.unsubscribe(topic)
                        logger.info(f"Client {client_id} unsubscribed from {topic}")
                    except MqttError as error:
                        logger.error(f"Client {client_id} failed to unsubscribe from {topic}: {error}")

    except asyncio.CancelledError:
        logger.info(f"Client {client_id} task cancelled. Unsubscribing and disconnecting...")
        # Attempt to unsubscribe if not already done
        if not shutdown_event_cas.is_set():
            for topic in topics:
                try:
                    await client.unsubscribe(topic)
                    logger.info(f"Client {client_id} unsubscribed from {topic}")
                except MqttError as error:
                    logger.error(f"Client {client_id} failed to unsubscribe from {topic}: {error}")
        raise
    except MqttError as error:
        logger.error(f"Client {client_id} encountered MQTT error: {error}")
    except Exception as e:
        logger.error(f"Client {client_id} encountered unexpected error: {e}")


# Main execution function
async def main(shutdown_main_event):
    start_time = datetime.utcnow()
    topics = generate_shared_topics()
    logger.info(f"Generated {len(topics)} shared subscription topics.")

    # Create tasks for each client
    tasks = [
        asyncio.create_task(connect_and_subscribe(client_num, topics, start_time, shutdown_main_event))
        for client_num in range(1, MAX_CLIENTS + 1)
    ]

    # Run all client tasks concurrently
    try:
        await asyncio.gather(*tasks)
        logger.info(f"Main finished {len(topics)} execution.")
    except asyncio.CancelledError:
        logger.info("All client tasks have been cancelled.")
    except Exception as ex:
        logger.error(f"An error occurred during execution: {ex}")


# Entry point
if __name__ == "__main__":
    asyncio.set_event_loop_policy(asyncio.WindowsSelectorEventLoopPolicy())
    loop = asyncio.get_event_loop()
    shutdown_event = asyncio.Event()

    # Register shutdown signals
    for sig in (signal.SIGINT, signal.SIGTERM):
        try:
            loop.add_signal_handler(sig, lambda: handle_shutdown(shutdown_event, loop))
        except NotImplementedError:
            # Signals are not implemented on Windows for some types
            signal.signal(sig, lambda s, f: handle_shutdown(shutdown_event, loop))

    try:
        loop.run_until_complete(main(shutdown_event))
    except KeyboardInterrupt:
        logger.info("Program interrupted by user.")
    finally:
        # Ensure all tasks are cancelled and the loop is closed
        pending = asyncio.all_tasks(loop)
        for task in pending:
            task.cancel()
        try:
            loop.run_until_complete(asyncio.gather(*pending, return_exceptions=True))
        except Exception as e:
            logger.error(f"Error during shutdown: {e}")
        loop.close()
        logger.info("Shutdown complete.")
