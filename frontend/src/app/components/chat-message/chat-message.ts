import { Component, Input } from '@angular/core';
import { ChatMessage as ChatMessageModel } from '../../models/chat.models';

@Component({
  selector: 'app-chat-message',
  imports: [],
  templateUrl: './chat-message.html',
  styleUrl: './chat-message.scss',
})
export class ChatMessage {
  @Input() message!: ChatMessageModel;
}
