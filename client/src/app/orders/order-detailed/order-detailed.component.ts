import { Component, OnInit } from '@angular/core';
import { IOrder } from 'src/app/shared/models/order';
import { OrdersService } from '../orders.service';
import { BreadcrumbService } from 'xng-breadcrumb';
import { ActivatedRoute } from '@angular/router';

@Component({
  selector: 'app-order-detailed',
  templateUrl: './order-detailed.component.html',
  styleUrls: ['./order-detailed.component.scss']
})
export class OrderDetailedComponent implements OnInit {
  order: IOrder;

  constructor(private orderService: OrdersService, private breadcrumbService: BreadcrumbService, private activatedRoute: ActivatedRoute) {
    this.breadcrumbService.set('@OrderDetailed', '');
  }

  ngOnInit(): void {
    this.orderService.getOrderDetailed(+this.activatedRoute.snapshot.paramMap.get('id'))
      .subscribe(
        order => {
          this.breadcrumbService.set('@OrderDetailed', `Order# ${order.id} - ${order.status}`);
          this.order = order;
        },
        err => {
          console.log(err);
        }
      );
  }

}
