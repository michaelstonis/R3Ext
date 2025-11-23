using System;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.Maui.Controls;
using R3;
using R3.DynamicData.List;

namespace R3Ext.SampleApp;

public class Product
{
    public string Name { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;
}

public class ProductGroup
{
    public string Category { get; set; } = string.Empty;

    public int Count { get; set; }

    public ObservableCollection<Product> Items { get; set; } = new();
}

#pragma warning disable CA1001
public partial class DDGroupingPage : ContentPage
#pragma warning restore CA1001
{
    private readonly SourceList<Product> _source = new();
    private readonly ObservableCollection<ProductGroup> _groups = new();
    private readonly IDisposable _groupSubscription;

    public DDGroupingPage()
    {
        InitializeComponent();
        GroupsView.ItemsSource = _groups;

        // Group products by category - simplified approach
        // For each change in source, rebuild all groups
        _groupSubscription = _source.Connect()
            .Subscribe(_ =>
            {
                _groups.Clear();
                var grouped = _source.Items.GroupBy(p => p.Category);
                foreach (var grp in grouped)
                {
                    _groups.Add(new ProductGroup
                    {
                        Category = grp.Key,
                        Count = grp.Count(),
                        Items = new ObservableCollection<Product>(grp),
                    });
                }
            });

        CategoryPicker.SelectedIndex = 0;
    }

    private void OnAddProduct(object sender, EventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(ProductNameEntry.Text) && CategoryPicker.SelectedIndex >= 0)
        {
            var product = new Product
            {
                Name = ProductNameEntry.Text,
                Category = CategoryPicker.Items[CategoryPicker.SelectedIndex],
            };
            _source.Add(product);
            ProductNameEntry.Text = string.Empty;
        }
    }

    private void OnAddSampleData(object sender, EventArgs e)
    {
        var products = new[]
        {
            new Product { Name = "Laptop", Category = "Electronics" },
            new Product { Name = "Phone", Category = "Electronics" },
            new Product { Name = "Tablet", Category = "Electronics" },
            new Product { Name = "C# Programming", Category = "Books" },
            new Product { Name = "Design Patterns", Category = "Books" },
            new Product { Name = "T-Shirt", Category = "Clothing" },
            new Product { Name = "Jeans", Category = "Clothing" },
            new Product { Name = "Apple", Category = "Food" },
            new Product { Name = "Banana", Category = "Food" },
        };
        _source.AddRange(products);
    }

    private void OnRemoveProduct(object sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is Product product)
        {
            _source.Remove(product);
        }
    }

    private void OnClear(object sender, EventArgs e)
    {
        _source.Clear();
    }
}
