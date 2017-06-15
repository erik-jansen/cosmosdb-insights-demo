using System;
using System.Collections.Generic;

namespace CosmosMetricsApp
{
    public class CollectionMeasurement
    {
        public string DatabaseId
        { get; set; }

        public string CollectionId
        { get; set; }

        public Dictionary<DateTime, Measurement> Measurements
        { get; set; }
    }
}