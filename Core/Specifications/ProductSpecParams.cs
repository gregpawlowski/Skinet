namespace Core.Specifications
{
    public class ProductSpecParams
    {
        private const int MaxPageSize = 50;
        public int PageIndex {get; set;} = 1;
        private int _pageSize = 6;
        public int PageSize 
        {
            get  => _pageSize;
            // Only allow a maximum of MaxPageSize when setting this.
            set => _pageSize = (value > MaxPageSize) ? MaxPageSize : value;
        }
        public int? BrandId { get; set; }
        public int? TypeId { get; set; }
        public string Sort { get; set; }

        private string _search;

        public string Search { 
            get => _search;
            // make sure search term is always lower case.
            set => _search = value.ToLower();
         }
    }
}