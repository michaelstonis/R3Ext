#pragma warning disable SA1413 // Use trailing comma in multi-line initializers
#pragma warning disable SA1516 // Elements should be separated by blank line
#pragma warning disable SA1629 // Documentation text should end with a period
#pragma warning disable CS8602 // Dereference of a possibly null reference
#pragma warning disable CS8618 // Non-nullable property must contain a non-null value when exiting constructor
#pragma warning disable CS9264 // Non-nullable property must contain a non-null value

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

public class SampleViewModel2
{
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

    public string Status { get; set; }

    public Person Person { get; set; } = new();

    public void Increment()
    {
        Counter++;
    }
}

public class SampleViewModel : ObservableObject
{
    private int _counter;
    private Person _person = new();
    private string _editableName = string.Empty;
    private string _status = string.Empty;

    // ==================== Deep Binding Demo Models ====================
    // Showcase all binding features: deep chains, null handling, mixed INPC/Plain, rewiring, etc.

    /// <summary>Leaf node with value properties (string and int)</summary>
    public sealed class DeepLeaf : ObservableObject
    {
        private string _name = "Default Leaf";
        private int _value = 0;

        public string Name
        {
            get => _name;
            set => this.SetProperty(ref _name, value);
        }

        public int Value
        {
            get => _value;
            set => this.SetProperty(ref _value, value);
        }
    }

    /// <summary>Level 5 - Contains nullable leaf to demo null handling</summary>
    public sealed class DeepE : ObservableObject
    {
        private DeepLeaf? _leaf;

        public DeepLeaf? Leaf
        {
            get => _leaf;
            set => this.SetProperty(ref _leaf, value);
        }
    }

    /// <summary>Level 4 - Contains nullable E to demo null propagation</summary>
    public sealed class DeepD : ObservableObject
    {
        private DeepE? _e;

        public DeepE? E
        {
            get => _e;
            set => this.SetProperty(ref _e, value);
        }
    }

    /// <summary>Level 3</summary>
    public sealed class DeepC : ObservableObject
    {
        private DeepD _d = new();

        public DeepD D
        {
            get => _d;
            set => this.SetProperty(ref _d, value);
        }
    }

    /// <summary>Level 2</summary>
    public sealed class DeepB : ObservableObject
    {
        private DeepC _c = new();

        public DeepC C
        {
            get => _c;
            set => this.SetProperty(ref _c, value);
        }
    }

    /// <summary>Level 1</summary>
    public sealed class DeepA : ObservableObject
    {
        private DeepB _b = new();

        public DeepB B
        {
            get => _b;
            set => this.SetProperty(ref _b, value);
        }
    }

    /// <summary>Root of deep INPC chain (6 levels deep)</summary>
    public sealed class DeepRoot : ObservableObject
    {
        private DeepA _a = new();

        public DeepA A
        {
            get => _a;
            set => this.SetProperty(ref _a, value);
        }
    }

    // ==================== Mixed Chain Demo: INPC -> Plain -> INPC ====================

    /// <summary>Plain (non-INPC) intermediate node - tests fallback to polling</summary>
    public sealed class PlainIntermediate
    {
        public Person? NotifyChild { get; set; }
        public int PlainValue { get; set; }
    }

    /// <summary>Root of mixed chain demonstrating INPC -> Plain -> INPC</summary>
    public sealed class MixedRoot : ObservableObject
    {
        private PlainIntermediate _plainNode = new();

        public PlainIntermediate PlainNode
        {
            get => _plainNode;
            set => this.SetProperty(ref _plainNode, value);
        }
    }

    // ==================== Null Handling Demo ====================

    /// <summary>Chain specifically designed to test null intermediate handling</summary>
    public sealed class NullableChainRoot : ObservableObject
    {
        private NullableIntermediate? _intermediate;

        public NullableIntermediate? Intermediate
        {
            get => _intermediate;
            set => this.SetProperty(ref _intermediate, value);
        }
    }

    public sealed class NullableIntermediate : ObservableObject
    {
        private Person? _target;

        public Person? Target
        {
            get => _target;
            set => this.SetProperty(ref _target, value);
        }
    }

    private DeepRoot _deep = new()
    {
        A = new DeepA
        {
            B = new DeepB
            {
                C = new DeepC
                {
                    D = new DeepD
                    {
                        E = new DeepE
                        {
                            Leaf = new DeepLeaf { Name = "Initial Leaf", Value = 42 }
                        }
                    }
                }
            }
        }
    };

    private MixedRoot _mixed = new()
    {
        PlainNode = new PlainIntermediate
        {
            NotifyChild = new Person { Name = "Mixed Start" },
            PlainValue = 100
        }
    };

    private NullableChainRoot _nullableChain = new()
    {
        Intermediate = new NullableIntermediate
        {
            Target = new Person { Name = "Nullable Start" }
        }
    };

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

    public NullableChainRoot NullableChain
    {
        get => _nullableChain;
        set => this.SetProperty(ref _nullableChain, value);
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
        if (Deep.A.B.C.D.E?.Leaf != null)
        {
            Deep.A.B.C.D.E.Leaf.Name = $"Deep {Counter}";
            Deep.A.B.C.D.E.Leaf.Value = Counter * 10;
        }

        // Toggle mixed child occasionally
        if (Counter % 2 == 0 && Mixed.PlainNode.NotifyChild != null)
        {
            Mixed.PlainNode.NotifyChild.Name = $"Mixed {Counter}";
        }

        // Demonstrate null handling
        if (Counter % 3 == 0 && NullableChain.Intermediate?.Target != null)
        {
            NullableChain.Intermediate.Target.Name = $"Nullable {Counter}";
        }
    }
}
