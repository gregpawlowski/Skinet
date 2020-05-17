import { Injectable } from '@angular/core';
import { environment } from 'src/environments/environment';
import { HttpClient } from '@angular/common/http';
import { map } from 'rxjs/operators';
import { IDeliveryMethod } from '../shared/models/deliveryMethod';
import { IOrderToCreate, IOrder } from '../shared/models/order';
import { BasketService } from '../basket/basket.service';

@Injectable({
  providedIn: 'root'
})
export class CheckoutService {
  baseUrl = environment.apiUrl;

  constructor(private http: HttpClient, private basketService: BasketService) { }

  getDeliveryMethods() {
    return this.http.get<IDeliveryMethod[]>(this.baseUrl + 'orders/deliverymethods')
      .pipe(
        map(res => res.sort((a, b) => b.price - a.price))
      );
  }

  createOrder(order: IOrderToCreate) {
    return this.http.post<IOrder>(this.baseUrl + 'orders', order);
  }
}
