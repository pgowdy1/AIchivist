import { Component, input, signal, effect, ViewChild, ElementRef, AfterViewChecked } from '@angular/core';
import { ChatService } from '../../services/chat';
import { ChatMessage as ChatMessageModel } from '../../models/chat.models';
import { ChatMessage } from '../chat-message/chat-message';

@Component({
  selector: 'app-chat-sidebar',
  imports: [ChatMessage],
  templateUrl: './chat-sidebar.html',
  styleUrl: './chat-sidebar.scss',
})
export class ChatSidebar implements AfterViewChecked {
  contextId = input('');
  visible = input(false);
  @ViewChild('messagesContainer') messagesContainer!: ElementRef<HTMLElement>;

  messages = signal<ChatMessageModel[]>([]);
  inputText = signal('');
  loading = signal(false);
  error = signal('');

  constructor(private chatService: ChatService) {
    // Reset messages when contextId changes
    effect(() => {
      this.contextId(); // track
      this.messages.set([]);
      this.error.set('');
    });
  }

  ngAfterViewChecked(): void {
    this.scrollToBottom();
  }

  private scrollToBottom(): void {
    try {
      const el = this.messagesContainer?.nativeElement;
      if (el) el.scrollTop = el.scrollHeight;
    } catch { /* ignore */ }
  }

  send(): void {
    const text = this.inputText().trim();
    if (!text || this.loading()) return;

    this.messages.update(msgs => [...msgs, { role: 'user' as const, content: text }]);
    this.inputText.set('');
    this.loading.set(true);
    this.error.set('');

    this.chatService.chat(this.messages(), this.contextId()).subscribe({
      next: (res) => {
        this.messages.update(msgs => [...msgs, { role: 'assistant' as const, content: res.response }]);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Failed to get a response. Please try again.');
        this.loading.set(false);
      }
    });
  }

  onKeyDown(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      this.send();
    }
  }
}
