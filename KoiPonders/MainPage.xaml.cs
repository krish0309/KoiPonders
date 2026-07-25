namespace KoiPonders
{
    public partial class MainPage : ContentPage
    {
        private readonly MapViewModel _viewModel;

        public MainPage(MapViewModel viewModel)
        {
            InitializeComponent();

            _viewModel = viewModel;
            BindingContext = _viewModel;

            mapView.GraphicsOverlays?.Add(_viewModel.FieldsOverlay);
            mapView.GeometryEditor = _viewModel.GeometryEditor;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _viewModel.LoadFieldsAsync();
        }
    }
}
