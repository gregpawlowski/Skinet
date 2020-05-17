import { Injectable } from '@angular/core';
import { environment } from 'src/environments/environment';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { IUser } from '../shared/models/user';
import { BehaviorSubject, ReplaySubject, of } from 'rxjs';
import { tap } from 'rxjs/operators';
import { getLocaleMonthNames } from '@angular/common';
import { Router } from '@angular/router';
import { IAddress } from '../shared/models/address';

@Injectable({
  providedIn: 'root'
})
export class AccountService {
  baseUrl = environment.apiUrl;

  // private currentUserSource = new BehaviorSubject<IUser>(undefined);
  // We're changing to a Replay Subject here. A replay subject does not emit an inital value of undefined.
  // Once we get a user it will be emited and cached and everything that subscribes to this will recieve that value.
  private currentUserSource = new ReplaySubject<IUser>(1);
  currentUser$ = this.currentUserSource.asObservable();

  constructor(private http: HttpClient, private router: Router) { }

  loadCurrentUser(token: string) {

    if (token === null) {
      this.currentUserSource.next(null);
      return of(null);
    }

    let headers = new HttpHeaders();
    headers = headers.set('Authorization', `Bearer ${token}`);

    return this.http.get<IUser>(this.baseUrl + 'account', {headers})
      .pipe(
        tap((user) => {
          if (user) {
            localStorage.setItem('token', user.token);
            this.currentUserSource.next(user);
          }
        })
      );
  }

  // Can't use value with replay subject.
  // getCurrentUserValue() {
  //   return this.currentUserSource.value;
  // }

  login(values: {username: string, password: string}) {
    return this.http.post<IUser>(this.baseUrl + 'account/login', values)
      .pipe(
        tap(user => {
          if (user) {
            localStorage.setItem('token', user.token);
            this.currentUserSource.next(user);
          }
        })
      );
  }

  register(values) {
    return this.http.post<IUser>(this.baseUrl + 'account/register', values)
      .pipe(
        tap(user => {
          if (user) {
            localStorage.setItem('token', user.token);
            this.currentUserSource.next(user);
          }
        })
      );
  }

  logout() {
    localStorage.removeItem('token');
    this.currentUserSource.next(undefined);
    this.router.navigateByUrl('/');
  }

  checkEmailExists(email: string) {
    return this.http.get<boolean>(this.baseUrl + 'account/emailexists?email=' + email);
  }

  getUserAddress() {
    return this.http.get<IAddress>(this.baseUrl + 'account/address');
  }

  updateUserAddress(address: IAddress) {
    return this.http.put<IAddress>(this.baseUrl + 'account/address', address);
  }
}
