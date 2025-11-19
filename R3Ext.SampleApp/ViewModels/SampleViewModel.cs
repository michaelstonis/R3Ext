using CommunityToolkit.Mvvm.ComponentModel;

namespace R3Ext.SampleApp.ViewModels;

public class Person : ObservableObject
{
    private string _name = string.Empty;

    public string Name
    {
        get => _name;
        set => this.SetProperty(ref _name, value);
    }
}

public class SampleViewModel : ObservableObject
{
    private int _counter;
    private Person _person = new();
    private string _editableName = string.Empty;
    private string _status = string.Empty;

    // Deep chain demo: DeepRoot -> A -> B -> C -> D -> Leaf.Name
    public sealed class DeepLeaf : ObservableObject
    {
        private string _name = string.Empty;

        public string Name
        {
            get => _name;
            set => this.SetProperty(ref _name, value);
        }
    }

    public sealed class DeepD : ObservableObject
    {
        private DeepLeaf _leaf = new();

        public DeepLeaf Leaf
        {
            get => _leaf;
            set => this.SetProperty(ref _leaf, value);
        }
    }

    public sealed class DeepC : ObservableObject
    {
        private DeepD _d = new();

        public DeepD D
        {
            get => _d;
            set => this.SetProperty(ref _d, value);
        }
    }

    public sealed class DeepB : ObservableObject
    {
        private DeepC _c = new();

        public DeepC C
        {
            get => _c;
            set => this.SetProperty(ref _c, value);
        }
    }

    public sealed class DeepA : ObservableObject
    {
        private DeepB _b = new();

        public DeepB B
        {
            get => _b;
            set => this.SetProperty(ref _b, value);
        }
    }

    public sealed class DeepRoot : ObservableObject
    {
        private DeepA _a = new();

        public DeepA A
        {
            get => _a;
            set => this.SetProperty(ref _a, value);
        }
    }

    // Mixed notify demo: NonNotifyParent (no INPC) -> Child(Person) -> Name
    public sealed class NonNotifyParent // deliberately NOT ObservableObject
    {
        public Person? Child { get; set; }
    }

    public sealed class MixedRoot : ObservableObject
    {
        private NonNotifyParent _nonNotify = new();

        public NonNotifyParent NonNotify
        {
            get => _nonNotify;
            set => this.SetProperty(ref _nonNotify, value);
        }
    }

    private DeepRoot _deep = new();
    private MixedRoot _mixed = new() { NonNotify = new NonNotifyParent { Child = new Person { Name = "Start", }, }, };

    public int Counter
    {
        get => _counter;
        set => this.SetProperty(ref _counter, value);
    }

    public Person Person
    {
        get => _person;
        set => this.SetProperty(ref _person, value);
    }

    public string EditableName
    {
        get => _editableName;
        set
        {
            if (this.SetProperty(ref _editableName, value))
            {
                if (Person.Name != value)
                {
                    Person.Name = value;
                }
            }
        }
    }

    public string Status
    {
        get => _status;
        private set => this.SetProperty(ref _status, value);
    }

    public DeepRoot Deep
    {
        get => _deep;
        set => this.SetProperty(ref _deep, value);
    }

    public MixedRoot Mixed
    {
        get => _mixed;
        set => this.SetProperty(ref _mixed, value);
    }

    public void Increment()
    {
        Counter++;
        Person.Name = "Name " + Counter;
        EditableName = Person.Name;
        Status = Counter switch
        {
            < 5 => "Low",
            < 10 => "Medium",
            _ => "High",
        };

        // mutate deep chain a bit for demo
        Deep.A.B.C.D.Leaf.Name = $"Deep {Counter}";

        // Toggle mixed child occasionally
        if (Counter % 2 == 0)
        {
            Mixed.NonNotify.Child = new Person { Name = $"Mixed {Counter}", };
        }
    }
}
