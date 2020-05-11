import { Injectable } from '@angular/core';
import { CanActivate, ActivatedRouteSnapshot, RouterStateSnapshot, Router } from '@angular/router';
import { Observable } from 'rxjs';
import { AccountService } from 'src/app/account/account.service';
import { map } from 'rxjs/operators';

@Injectable({
  providedIn: 'root'
})
export class AuthGuard implements CanActivate {

  constructor(private accountService: AccountService, private router: Router) {}

  canActivate(
    next: ActivatedRouteSnapshot,
    state: RouterStateSnapshot): Observable<boolean> {

    return this.accountService.currentUser$
      .pipe(
        map(auth => {
          if (auth) {
            return true;
          }
          // If user is not logged in then return then save the URL to send the user back to
          // and pass it as a queryParam to the login controller.
          this.router.navigate(['account/login'], { queryParams: { returnUrl: state.url}});
        })
      );
  }
}
