using CsvHelper.Configuration.Attributes;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AddExif.Models
{
    class ImageInfo : BindableBase
    {
        private string status;


        public string FileName { get; set; }
        public string NewFileName { get; set; }
        public string Title { get; set; }
        public string Subject { get; set; }
        public string Rating { get; set; }
        public string Tags { get; set; }
        public string Comments { get; set; }
        public string Latitude { get; set; }
        public string Longitude { get; set; }
        public string Author { get; set; }

        [Ignore]
        public string Status { get => status; set => SetProperty(ref status, value); }
    }
}
