import { NgModule } from '@angular/core';
import { Routes, RouterModule } from '@angular/router';
import { TestErrorComponent } from './core/test-error/test-error.component';
import { ServerErrorComponent } from './core/server-error/server-error.component';
import { NotFoundComponent } from './core/not-found/not-found.component';

const routes: Routes = [
  { path: '', loadChildren: () => import('./home/home.module').then(m => m.HomeModule), pathMatch: 'full', data: {breadcrumb: 'Home'}},
  { path: 'test-error', component: TestErrorComponent, data: {breadcrumb: 'Test Errors'}},
  { path: 'server-error', component: ServerErrorComponent, data: {breadcrumb: 'Server Error'}},
  { path: 'not-found', component: NotFoundComponent, data: {breadcrumb: 'Not Found'}},
  { path: 'shop',  loadChildren: () => import('./shop/shop.module').then(m => m.ShopModule), data: {breadcrumb: 'Shop'}},
  { path: '**', redirectTo: 'not-found', pathMatch: 'full'}
];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule { }
