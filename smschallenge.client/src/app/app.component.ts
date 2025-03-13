import { HttpClient, HttpParams } from '@angular/common/http';
import { Component, OnInit } from '@angular/core';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

interface Message {
  message: string;
  account_number: number;
  phone_number: number;
}

interface MessageResponse {
  message: string;
}

interface Heartbeat {
  msg_count: number;
}

interface PhoneStats {
  phone_number: number;
  last_updated: Date;
  success_count: number;
  failure_count: number;
}

interface AccountStats {
  account_number: number;
  phone_stats: { [key: number]: PhoneStats };
  phone_numbers: string;
  last_updated: Date;
  success_count: boolean;
  failure_count: boolean;
}

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  standalone: false,
  styleUrl: './app.component.css'
})

export class AppComponent implements OnInit {
  stats: AccountStats[] = [];
  msg_count: number = 0;
  phoneNumber?: number;
  accountNumber?: number;
  startDate?: Date;
  endDate?: Date;

  constructor(private http: HttpClient, private messageService: MessageService) {}

  async ngOnInit() {
    await this.PopulateData();
  }

  async PopulateData() {
    const messages: Message[] = [];
    for (let i = 0; i < 10; i++) {
      messages.push({
        message: `Message ${i + 1}`,
        account_number: 123456 + i,
        phone_number: 1234567890 + i
      });
      messages.push({
        message: `Message ${i + 1}`,
        account_number: 123456 + i,
        phone_number: 1234567891 + i
      });
    }

    console.log(messages);
    this.http.post<MessageResponse>('/Message/send', messages).subscribe(
      (result) => {
        console.log(result);
      },
      (error) => {
        console.error(error);
      }
    );

    setInterval(() => {
      this.fetchStats();
      this.getHeartbeat();
    }, 1000);
  }

  fetchStats() {
    this.messageService.getStats(this.phoneNumber, this.accountNumber, this.startDate, this.endDate).subscribe(stats => {
      this.stats = stats;
      this.stats.forEach(stat => {
        stat.phone_numbers = Object.values(stat.phone_stats).map(phoneStat => phoneStat.phone_number).join(', ');
      });
    });
  }

  getHeartbeat() {
    this.messageService.getHeartbeat().subscribe(response => {
      this.msg_count = response.msg_count;
    });
  }

  title = 'smschallenge.client';
}

@Injectable({
  providedIn: 'root'
})

export class MessageService {
  constructor(private http: HttpClient) { }

  getStats(phoneNumber?: number, accountNumber?: number, startDate?: Date, endDate?: Date): Observable<AccountStats[]> {
    let params = new HttpParams();
    if (phoneNumber) params = params.append('phoneNumber', phoneNumber.toString());
    if (accountNumber) params = params.append('accountNumber', accountNumber.toString());
    if (startDate) params = params.append('startDate', startDate.toString());
    if (endDate) params = params.append('endDate', endDate.toString());

    return this.http.get<AccountStats[]>('/Message/stats', { params });
  }

  getHeartbeat(): Observable<Heartbeat> {
    return this.http.get<Heartbeat>('/Message/heartbeat');
  }
}
