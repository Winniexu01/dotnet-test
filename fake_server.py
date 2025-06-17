import socket
import threading

HOST='0.0.0.0'
PORT=8080
CONTENT_LENGTH=10 * 1024 * 1024  # 10 MB
SEND_LENGTH=2 * 1024 * 1024  # 2 MB

def handle_client(conn):
    try:
        request = conn.recv(1024)
        print(f"Received request:")
        print(request.decode(errors='ignore'))
        
        response_headers = (
            "HTTP/1.1 200 OK\r\n"
            f"Content-Length: {CONTENT_LENGTH}\r\n"
            "Content-Type: application/octet-stream\r\n"
            "Connection: close\r\n"
            "\r\n"
        )
        conn.sendall(response_headers.encode())

        chunk = b'\0' * 4096
        total_sent = 0
        while total_sent < SEND_LENGTH:
            conn.sendall(chunk)
            total_sent += len(chunk)
        print(f"Sent {total_sent} bytes, then closed connection.")
    finally:
        conn.close()
def run_server():
    with socket.create_server((HOST, PORT)) as server:
        print(f"Fake HTTP server listening on {HOST}:{PORT}")
        while True:
            conn, addr = server.accept()
            threading.Thread(target=handle_client, args=(conn,), daemon=True).start()

if __name__ == "__main__":
    run_server()