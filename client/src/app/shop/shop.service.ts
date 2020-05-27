import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { IPagination, Pagination } from '../shared/models/pagination';
import { IBrand } from '../shared/models/brand';
import { IType } from '../shared/models/productType';
import { ShopParams } from '../shared/models/shopParams';
import { IProduct } from '../shared/models/product';
import { tap } from 'rxjs/operators';
import { of } from 'rxjs';
import { environment } from 'src/environments/environment';

@Injectable({
  providedIn: 'root'
})
export class ShopService {
  baseUrl = environment.apiUrl;
  products: IProduct[] = [];
  brands: IBrand[];
  types: IType[];
  // Move these into the service
  pagination = new Pagination();
  shopParams = new ShopParams();

  constructor(private http: HttpClient) { }

  getProducts(useCache: boolean) {

    // If we are doing filtering we need to reset our cache.
    if (useCache === false) {
      this.products = [];
    }

    // Otherwise we can check if we have products
    if (this.products.length > 0 && useCache === true) {
      // Check how many pages we currently have of products.
      const pagesReceived = Math.ceil(this.products.length / this.shopParams.pageSize);

      // If the number of pages we have is
      // more or equal to the page number being requested then we can return from the service without going to the API.
      if (this.shopParams.pageNumber <= pagesReceived) {
        this.pagination.data =
          this.products.slice(
            (this.shopParams.pageNumber - 1) * this.shopParams.pageSize,
            this.shopParams.pageNumber * this.shopParams.pageSize
          );

        return of(this.pagination);
      }
    }

    let params = new HttpParams();

    if (this.shopParams.brandId !== 0) {
      params = params.append('brandId', this.shopParams.brandId.toString());
    }
    if (this.shopParams.typeId !== 0) {
      params = params.append('typeId', this.shopParams.typeId.toString());
    }

    if (this.shopParams.search) {
      params = params.append('search', this.shopParams.search);
    }

    params = params.append('sort', this.shopParams.sort);

    params = params.append('pageIndex', this.shopParams.pageNumber.toString());
    params = params.append('pageSize', this.shopParams.pageSize.toString());

    return this.http.get<IPagination>(this.baseUrl + 'products', {params})
      .pipe(
        tap(response => {
          this.products = [ ...this.products, ...response.data];
          this.pagination = response;
        })
      );
  }

  setShopParams(params: ShopParams) {
    this.shopParams = params;
  }

  getShopParams() {
    return {...this.shopParams};
  }

  getProduct(id: number) {
    const product = this.products.find(p => p.id === id);

    // If we have the product in our cache we can return it.
    if (product) {
      return of(product);
    }
    return this.http.get<IProduct>(this.baseUrl + 'products/' + id);
  }

  getBrands() {
    if (this.brands) {
      return of(this.brands);
    }

    return this.http.get<IBrand[]>(this.baseUrl + 'products/brands')
      .pipe(tap(brands => this.brands = brands));
  }

  getTypes() {
    if (this.types) {
      return of(this.types);
    }

    return this.http.get<IType[]>(this.baseUrl + 'products/types')
      .pipe(tap(types => this.types = types));
  }

}
