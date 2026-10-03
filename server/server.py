import http.server
import socketserver
import json
import os
import sys
import mimetypes
import shutil
from datetime import datetime

# Windows 콘솔 인코딩 호환성 처리 (cp949 이모지 에러 방지)
if sys.platform == "win32":
    try:
        sys.stdout.reconfigure(encoding="utf-8")
        sys.stderr.reconfigure(encoding="utf-8")
    except Exception:
        pass

PORT = int(os.environ.get("PORT", 5000))
BASE_DIR = os.path.dirname(os.path.abspath(__file__))
MANIFEST_PATH = os.path.join(BASE_DIR, "manifest.json")
BACKUP_PATH = os.path.join(BASE_DIR, "manifest.backup.json")
WEB_DIR = os.path.join(BASE_DIR, "web")

class AutoDownloaderHandler(http.server.BaseHTTPRequestHandler):
    def end_headers(self):
        # 모든 응답에 CORS 허용 헤더 추가 (클라이언트 프로그램 및 외부 연동 대비)
        self.send_header("Access-Control-Allow-Origin", "*")
        self.send_header("Access-Control-Allow-Methods", "GET, POST, OPTIONS")
        self.send_header("Access-Control-Allow-Headers", "Content-Type, Authorization")
        self.send_header("Cache-Control", "no-cache, no-store, must-revalidate")
        super().end_headers()

    def do_OPTIONS(self):
        self.send_response(204)
        self.end_headers()

    def do_GET(self):
        path = self.path.split("?")[0].rstrip("/")
        if not path:
            path = "/"

        # API: 매니페스트 반환 (클라이언트 다운로더 및 관리자 웹 UI용)
        if path == "/api/manifest":
            self.serve_manifest()
            return

        # API: 서버 상태 확인
        if path == "/api/status":
            self.serve_status()
            return

        # 웹 대시보드 정적 파일 서빙
        if path == "/" or path == "/index.html":
            self.serve_file(os.path.join(WEB_DIR, "index.html"), "text/html; charset=utf-8")
            return

        # web 디렉토리 내 정적 파일 (css, js, assets 등)
        relative_path = path.lstrip("/")
        requested_file = os.path.join(WEB_DIR, relative_path)
        if os.path.isfile(requested_file):
            content_type, _ = mimetypes.guess_type(requested_file)
            if not content_type:
                content_type = "application/octet-stream"
            if content_type.startswith("text/") or content_type == "application/javascript":
                content_type += "; charset=utf-8"
            self.serve_file(requested_file, content_type)
            return

        # 404 처리
        self.send_error(404, "Page or endpoint not found")

    def do_POST(self):
        path = self.path.split("?")[0].rstrip("/")
        if path == "/api/manifest":
            self.save_manifest()
            return

        self.send_error(404, "Endpoint not found")

    def serve_manifest(self):
        try:
            if not os.path.exists(MANIFEST_PATH):
                self.send_json({"error": "manifest.json does not exist"}, 404)
                return

            with open(MANIFEST_PATH, "r", encoding="utf-8") as f:
                data = json.load(f)
            self.send_json(data, 200)
        except Exception as e:
            self.send_json({"error": str(e)}, 500)

    def serve_status(self):
        file_count = 0
        pack_name = "Unknown"
        version = "Unknown"
        if os.path.exists(MANIFEST_PATH):
            try:
                with open(MANIFEST_PATH, "r", encoding="utf-8") as f:
                    manifest = json.load(f)
                    file_count = len(manifest.get("files", []))
                    pack_name = manifest.get("pack_name", "")
                    version = manifest.get("version", "")
            except Exception:
                pass

        status_info = {
            "status": "online",
            "server_time": datetime.now().isoformat(),
            "pack_name": pack_name,
            "version": version,
            "file_count": file_count,
            "api_endpoint": "/api/manifest"
        }
        self.send_json(status_info, 200)

    def save_manifest(self):
        try:
            content_length = int(self.headers.get("Content-Length", 0))
            if content_length <= 0:
                self.send_json({"error": "Empty body"}, 400)
                return

            post_data = self.rfile.read(content_length)
            data = json.loads(post_data.decode("utf-8"))

            # 기본 유효성 검증
            if "files" not in data or not isinstance(data["files"], list):
                self.send_json({"error": "'files' array is required in manifest"}, 400)
                return

            # 이전 파일이 있으면 백업
            if os.path.exists(MANIFEST_PATH):
                shutil.copy2(MANIFEST_PATH, BACKUP_PATH)

            # 저장
            with open(MANIFEST_PATH, "w", encoding="utf-8") as f:
                json.dump(data, f, ensure_ascii=False, indent=2)

            print(f"[{datetime.now().strftime('%H:%M:%S')}] 매니페스트 저장 완료: {len(data['files'])}개 모드 등록됨")
            self.send_json({"success": True, "message": "성공적으로 저장되었습니다.", "file_count": len(data["files"])}, 200)
        except Exception as e:
            self.send_json({"error": f"저장 실패: {str(e)}"}, 500)

    def serve_file(self, filepath, content_type):
        try:
            with open(filepath, "rb") as f:
                content = f.read()
            self.send_response(200)
            self.send_header("Content-Type", content_type)
            self.send_header("Content-Length", str(len(content)))
            self.end_headers()
            self.wfile.write(content)
        except Exception as e:
            self.send_error(500, f"Error reading file: {str(e)}")

    def send_json(self, data, status_code=200):
        body = json.dumps(data, ensure_ascii=False).encode("utf-8")
        self.send_response(status_code)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def log_message(self, format, *args):
        # 깔끔한 터미널 출력을 위한 간소화된 로그
        sys.stdout.write(f"[{datetime.now().strftime('%H:%M:%S')}] {args[0]} - {args[1]}\n")
        sys.stdout.flush()

class ThreadingHTTPServer(socketserver.ThreadingMixIn, http.server.HTTPServer):
    daemon_threads = True

def run(port=PORT):
    server_address = ("", port)
    httpd = ThreadingHTTPServer(server_address, AutoDownloaderHandler)
    print("=" * 60)
    print(f"[START] 게임 모드 간편 다운로더 관리자 서버가 시작되었습니다.")
    print(f"-> 관리자 웹 대시보드:  http://localhost:{port}")
    print(f"-> 클라이언트 API:     http://localhost:{port}/api/manifest")
    print(f"   (외부 접속 시 서버 IP 또는 포트포워딩/도메인 주소를 사용하세요)")
    print("=" * 60)
    print("서버를 종료하려면 Ctrl+C 를 누르세요.\n")
    try:
        httpd.serve_forever()
    except KeyboardInterrupt:
        print("\n서버를 종료합니다.")
        httpd.server_close()

if __name__ == "__main__":
    p = PORT
    if len(sys.argv) > 1:
        try:
            p = int(sys.argv[1])
        except ValueError:
            pass
    run(p)
