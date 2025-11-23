using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.Maui.Controls;
using R3;
using R3.DynamicData.Cache;
using R3.DynamicData.List;

namespace R3Ext.SampleApp;

#pragma warning disable CA1001
public partial class DynamicDataDistinctValuesPage : ContentPage
#pragma warning restore CA1001
{
    private readonly SourceCache<ProductItem, int> _productsCache;
    private readonly List<IDisposable> _subscriptions = new();
    private int _nextId = 1;
    private bool _trackingCategories = true;

    public DynamicDataDistinctValuesPage()
    {
        InitializeComponent();
        _productsCache = new SourceCache<ProductItem, int>(p => p.Id);
        SetupBindings();
        UpdateButtonStates();
    }

    private void SetupBindings()
    {
        // Bind products to the products view
        var productsCollection = new ObservableCollection<ProductItem>();
        ProductsView.ItemsSource = productsCollection;

        _subscriptions.Add(
            _productsCache.Connect()
                .Bind(productsCollection));

        // Track products count
        _subscriptions.Add(
            _productsCache.Connect()
                .QueryWhenChanged(q => q.Count)
                .Subscribe(count => ProductsCountLabel.Text = count.ToString()));

        // Set up initial distinct values binding (categories)
        UpdateDistinctValuesBinding();
    }

    private void UpdateDistinctValuesBinding()
    {
        // Create new observable collection for distinct values
        var distinctCollection = new ObservableCollection<string>();
        DistinctView.ItemsSource = distinctCollection;

        // Create DistinctValues observable based on current tracking mode
        var distinctObservable = _trackingCategories
            ? _productsCache.Connect().DistinctValues(p => p.Category)
            : _productsCache.Connect().DistinctValues(p => p.Brand);

        // Update UI labels
        DistinctTitleLabel.Text = _trackingCategories ? "Unique Categories" : "Unique Brands";
        ModeLabel.Text = _trackingCategories ? "Tracking: Categories" : "Tracking: Brands";

        // Bind to collection and track count
        _subscriptions.Add(distinctObservable.Bind(distinctCollection));

        _subscriptions.Add(
            distinctObservable
                .Subscribe(changes =>
                {
                    DistinctCountLabel.Text = distinctCollection.Count.ToString();
                }));
    }

    private void OnAddOrUpdateProduct(object? sender, EventArgs e)
    {
        if (!int.TryParse(ProductIdEntry.Text, out var id))
        {
            id = _nextId++;
        }

        var name = ProductNameEntry.Text?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            name = $"Product {id}";
        }

        var category = CategoryEntry.Text?.Trim();
        if (string.IsNullOrEmpty(category))
        {
            category = "General";
        }

        var brand = BrandEntry.Text?.Trim();
        if (string.IsNullOrEmpty(brand))
        {
            brand = "Generic";
        }

        var product = new ProductItem
        {
            Id = id,
            Name = name,
            Category = category,
            Brand = brand,
        };

        _productsCache.AddOrUpdate(product);

        // Update next ID if needed
        if (id >= _nextId)
        {
            _nextId = id + 1;
        }

        // Clear inputs
        ProductIdEntry.Text = string.Empty;
        ProductNameEntry.Text = string.Empty;
        CategoryEntry.Text = string.Empty;
        BrandEntry.Text = string.Empty;
    }

    private void OnAddBatch(object? sender, EventArgs e)
    {
        var sampleData = new[]
        {
            new { Name = "iPhone 15", Category = "Electronics", Brand = "Apple" },
            new { Name = "Galaxy S24", Category = "Electronics", Brand = "Samsung" },
            new { Name = "MacBook Pro", Category = "Electronics", Brand = "Apple" },
            new { Name = "Office Chair", Category = "Furniture", Brand = "IKEA" },
            new { Name = "Desk Lamp", Category = "Furniture", Brand = "IKEA" },
            new { Name = "Running Shoes", Category = "Sports", Brand = "Nike" },
            new { Name = "Basketball", Category = "Sports", Brand = "Wilson" },
            new { Name = "Yoga Mat", Category = "Sports", Brand = "Nike" },
        };

        foreach (var data in sampleData)
        {
            _productsCache.AddOrUpdate(new ProductItem
            {
                Id = _nextId++,
                Name = data.Name,
                Category = data.Category,
                Brand = data.Brand,
            });
        }
    }

    private void OnRemoveProduct(object? sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is ProductItem product)
        {
            _productsCache.Remove(product.Id);
        }
    }

    private void OnClearAll(object? sender, EventArgs e)
    {
        _productsCache.Clear();
        _nextId = 1;
    }

    private void OnShowCategories(object? sender, EventArgs e)
    {
        if (!_trackingCategories)
        {
            _trackingCategories = true;
            UpdateDistinctValuesBinding();
            UpdateButtonStates();
        }
    }

    private void OnShowBrands(object? sender, EventArgs e)
    {
        if (_trackingCategories)
        {
            _trackingCategories = false;
            UpdateDistinctValuesBinding();
            UpdateButtonStates();
        }
    }

    private void UpdateButtonStates()
    {
        CategoriesButton.BackgroundColor = _trackingCategories
            ? Color.FromArgb("#512BD4")
            : Color.FromArgb("#2B0B98");
        BrandsButton.BackgroundColor = !_trackingCategories
            ? Color.FromArgb("#512BD4")
            : Color.FromArgb("#2B0B98");
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        foreach (var sub in _subscriptions)
        {
            sub?.Dispose();
        }

        _subscriptions.Clear();
        _productsCache.Dispose();
    }
}

public class ProductItem : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    private int _id;
    private string _name = string.Empty;
    private string _category = string.Empty;
    private string _brand = string.Empty;

    public int Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string Category
    {
        get => _category;
        set => SetProperty(ref _category, value);
    }

    public string Brand
    {
        get => _brand;
        set => SetProperty(ref _brand, value);
    }
}
