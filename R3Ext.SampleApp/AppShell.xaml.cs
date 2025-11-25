namespace R3Ext.SampleApp;

public partial class AppShell : Shell
{
    public AppShell()
    {
        this.InitializeComponent();

        // Register R3 Core pages for navigation
        Routing.RegisterRoute(nameof(BasicsPage), typeof(BasicsPage));
        Routing.RegisterRoute(nameof(DeepBindingPage), typeof(DeepBindingPage));
        Routing.RegisterRoute(nameof(WhenObservedPage), typeof(WhenObservedPage));
        Routing.RegisterRoute(nameof(ConverterPlaygroundPage), typeof(ConverterPlaygroundPage));
        Routing.RegisterRoute(nameof(ConversionFormPage), typeof(ConversionFormPage));
        Routing.RegisterRoute(nameof(ControlsPage), typeof(ControlsPage));
        Routing.RegisterRoute(nameof(FormPage), typeof(FormPage));
        Routing.RegisterRoute(nameof(PerformancePage), typeof(PerformancePage));
        Routing.RegisterRoute(nameof(CommandsPage), typeof(CommandsPage));

        // Register DynamicData pages for navigation
        Routing.RegisterRoute(nameof(DynamicDataBasicsPage), typeof(DynamicDataBasicsPage));
        Routing.RegisterRoute(nameof(DynamicDataCachePage), typeof(DynamicDataCachePage));
        Routing.RegisterRoute(nameof(DynamicDataFilterSortPage), typeof(DynamicDataFilterSortPage));
        Routing.RegisterRoute(nameof(DynamicDataOperatorsPage), typeof(DynamicDataOperatorsPage));
        Routing.RegisterRoute(nameof(DDAggregationPage), typeof(DDAggregationPage));
        Routing.RegisterRoute(nameof(DDTransformationPage), typeof(DDTransformationPage));
        Routing.RegisterRoute(nameof(DynamicDataTransformManyPage), typeof(DynamicDataTransformManyPage));
        Routing.RegisterRoute(nameof(DynamicDataDistinctValuesPage), typeof(DynamicDataDistinctValuesPage));
        Routing.RegisterRoute(nameof(DDRefreshPage), typeof(DDRefreshPage));
        Routing.RegisterRoute(nameof(DDLogicalOperatorsPage), typeof(DDLogicalOperatorsPage));
        Routing.RegisterRoute(nameof(DDGroupingPage), typeof(DDGroupingPage));
        Routing.RegisterRoute(nameof(DDLifecyclePage), typeof(DDLifecyclePage));
    }
}
