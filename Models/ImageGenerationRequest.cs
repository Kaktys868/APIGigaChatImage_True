using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGigaChatImage_True.Models
{
    public class ImageGenerationRequest
    {
        public string model { get; set; } = "GigaChat";
        public string prompt { get; set; }
        public int n { get; set; } = 1;
        public string size { get; set; } = "1024x1024";
        public string response_format { get; set; } = "b64_json";
    }

    public class ImageGenerationResponse
    {
        public long created { get; set; }
        public List<ImageData> data { get; set; }
    }

    public class ImageData
    {
        public string b64_json { get; set; }
        public string url { get; set; }
    }
}
