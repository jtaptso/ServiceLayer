using ServiceLayer.DTW.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace ServiceLayer.DTW.Infrastructure.Parsing
{
    public class FileParserResolver : IFileParserResolver
    {
        private readonly IEnumerable<IFileParser> _parsers;

        public FileParserResolver(IEnumerable<IFileParser> parsers)
        {
            _parsers = parsers;
        }

        public IFileParser Resolve(string fileName)
        {
            return _parsers.FirstOrDefault(p => p.Supports(fileName)) 
                ?? throw new NotSupportedException($"No parser found for file: {fileName}");
        }
    }
}
