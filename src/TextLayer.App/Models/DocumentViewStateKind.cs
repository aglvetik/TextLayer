namespace TextLayer.App.Models;

public enum DocumentViewStateKind
{
    Empty = 0,
    LoadingImage = 1,
    Recognizing = 2,
    Ready = 3,
    NoTextFound = 4,
    Error = 5,
}
