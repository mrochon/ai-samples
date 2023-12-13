// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace SemanticKernel1
{
    public class Settings
    {
        public string? Model { get; set; }
        public string? Endpoint { get; set; }
        public string? Secret { get; set; }
    }
}
