import { Component, OnInit, ViewChild, ElementRef } from '@angular/core';
import { IProduct } from '../shared/models/product';
import { ShopService } from './shop.service';
import { IBrand } from '../shared/models/brand';
import { IType } from '../shared/models/productType';
import { ShopParams } from '../shared/models/shopParams';

@Component({
  selector: 'app-shop',
  templateUrl: './shop.component.html',
  styleUrls: ['./shop.component.scss']
})
export class ShopComponent implements OnInit {
  @ViewChild('search', {static: false}) searchTerm: ElementRef;
  products: IProduct[];
  types: IType[];
  brands: IBrand[];
  shopParams: ShopParams;
  totalCount: number;
  sortOptions = [
    { name: 'Alphabetical', value: 'name'},
    { name: 'Price: Low to High', value: 'priceAsc'},
    { name: 'Price: Hight to Low', value: 'priceDesc'}
  ];

  constructor(private shopService: ShopService) {
    this.shopParams = this.shopService.getShopParams();
  }

  ngOnInit(): void {
    this.getProducts(true);
    this.getBrands();
    this.getTypes();
  }

  getProducts(useCache = false) {
    this.shopService.getProducts(useCache)
    .subscribe(
      (res) => {
        this.products = res.data;
        this.totalCount = res.count;
        },
      (err) => console.log(err));
  }

  getTypes() {
    this.shopService.getTypes()
      .subscribe(
        (res) => this.types = [{ id: 0, name: 'All' }, ...res],
        (err) => console.log(err)
      );
  }

  getBrands() {
    this.shopService.getBrands()
      .subscribe(
        (res) => this.brands = [{ id: 0, name: 'All' }, ...res],
        (err) => console.log(err)
      );
  }

  onBrandSelected(brandId: number) {
    this.shopParams.brandId = brandId;
    this.shopParams.pageNumber = 1;
    this.shopService.setShopParams(this.shopParams);
    this.shopParams = this.shopService.getShopParams();
    this.getProducts();
  }

  onTypeSelected(typeId: number) {
    this.shopParams.typeId = typeId;
    this.shopParams.pageNumber = 1;
    this.shopService.setShopParams(this.shopParams);
    this.shopParams = this.shopService.getShopParams();
    this.getProducts();
  }

  onSortSelected(sort: string) {
    this.shopParams.sort = sort;
    this.shopService.setShopParams(this.shopParams);
    this.getProducts();
  }

  onPageChanged(page: number) {

    if (this.shopParams.pageNumber !== page) {
      this.shopParams.pageNumber = page;
      this.shopService.setShopParams(this.shopParams);
      this.shopParams = this.shopService.getShopParams();
      this.getProducts(true);
    }
  }

  onSearch() {
    this.shopParams.search = this.searchTerm.nativeElement.value;
    this.shopParams.pageNumber = 1;
    this.shopService.setShopParams(this.shopParams);
    this.shopParams = this.shopService.getShopParams();
    this.getProducts();
  }

  onReset() {
    this.searchTerm.nativeElement.value = '';
    this.shopParams = new ShopParams();
    this.shopService.setShopParams(this.shopParams);
    this.shopParams = this.shopService.getShopParams();
    this.getProducts();
  }

}
