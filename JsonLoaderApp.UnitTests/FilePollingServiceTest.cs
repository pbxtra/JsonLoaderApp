using JsonLoaderApp.Models;
using JsonLoaderApp.Services;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Time.Testing;

namespace JsonLoaderApp.UnitTests
{
    public sealed class FilePollingServiceTest
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        [Fact]
        public async Task Read_and_update_data_from_json()
        {
            var directory = Path.Combine(
                Path.GetTempPath(),
                Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(directory);

            var path = Path.Combine(directory, "sensors.json");
            var fakeTime = new FakeTimeProvider();

            var checks = Channel.CreateUnbounded<bool>();
            var received = Channel.CreateUnbounded<List<SensorData>>();

            try
            {

                //Arange -----
                await WriteTestDataAsync(path, CreateData("sensor-1", 45.2m));

                //service will call Dispose when out of scope
                await using var service = new FilePollingService(
                    path,
                    interval: TimeSpan.FromSeconds(2),
                    timeProvider: fakeTime,
                    afterCheck: () => checks.Writer.TryWrite(true));

                //Act -----

                //Start service and wait for first file read
                service.Start();

                //Wait for signal from service
                await ReadChannelAsync(checks);

                //Subscribe after first event.
                service.FileChanged += (_, sensorList) =>
                {
                    received.Writer.TryWrite(sensorList);
                };

                //Assert -----

                // Set stubTime to advance 
                fakeTime.Advance(TimeSpan.FromSeconds(3));

                //Wait for signal from service
                await ReadChannelAsync(checks);

                //We will not have a new event, file is unchanged.
                Assert.False(received.Reader.TryRead(out _));

                // Changing file and set new time 
                await WriteTestDataAsync(path, CreateData("sensor-2", 61.7m));
                File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(3));

                //Set stubTime to advance. Wait for file read 
                fakeTime.Advance(TimeSpan.FromSeconds(3));

                //Wait for signal from service
                await ReadChannelAsync(checks);

                //Read event and check if we got the correct value. 
                var second = await ReadChannelAsync(received);
                var updatedSensor = Assert.Single(second);

                Assert.Equal("sensor-2", updatedSensor.Id);
                Assert.Equal(61.7m, updatedSensor.HumidityPercent);

            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        private static SensorDataCollection CreateData(
            string id,
            decimal humidity) =>
            new()
            {
                SensorDataList =
                [
                    new SensorData
                {
                    Id = id,
                    Timestamp = new DateTime(
                        2026, 10, 6, 12, 0, 0, DateTimeKind.Utc),
                    HumidityPercent = humidity
                }
                ]
            };

        private static async Task WriteTestDataAsync(string path, SensorDataCollection data)
        {
            var json = JsonSerializer.Serialize(data, JsonOptions);
            await File.WriteAllTextAsync(path, json);
        }

        private static async Task<T> ReadChannelAsync<T>(Channel<T> channel)
        {
            using var timeout = new CancellationTokenSource(
                TimeSpan.FromSeconds(3));

            return await channel.Reader.ReadAsync(timeout.Token);
        }
    }
}
