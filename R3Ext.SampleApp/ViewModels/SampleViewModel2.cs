namespace R3Ext.SampleApp.ViewModels;

public class SampleViewModel2
{
    public SampleViewModel2()
    {
        EditableName = string.Empty;
        Status = string.Empty;
    }

    public int Counter { get; set; }

    public string EditableName
    {
        get => field;
        set
        {
            field = value;
            Person?.Name = value;
        }
    }

    public string Status { get; set; } = string.Empty;

    public Person Person { get; set; } = new();

    public void Increment()
    {
        Counter++;
    }
}
