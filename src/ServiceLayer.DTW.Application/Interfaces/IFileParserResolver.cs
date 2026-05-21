namespace ServiceLayer.DTW.Application.Interfaces;

public interface IFileParserResolver
{
    IFileParser Resolve(string fileName);
}
