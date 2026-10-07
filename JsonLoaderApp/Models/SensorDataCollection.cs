using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace JsonLoaderApp.Models
{
    public class SensorDataCollection
    {
        [JsonPropertyName("measurements")]
        public List<SensorData> SensorDataList { get; set; } = [];
    }
}
