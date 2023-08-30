namespace TumblerTags.Model;

public partial class SmartCardReader : ObservableObject
{
    [ObservableProperty]
    string name;

    [ObservableProperty]
    bool smartCardPresent;

    [ObservableProperty]
    SmartCard smartCard;
}
