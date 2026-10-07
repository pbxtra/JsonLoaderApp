using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace JsonLoaderApp.Models
{
    public class SensorData
    {
        [JsonPropertyName("id")]
        public required string Id { get; set; }

        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; }

        [JsonPropertyName("humidity_percent")]
        public decimal HumidityPercent { get; set; }
    }
}
