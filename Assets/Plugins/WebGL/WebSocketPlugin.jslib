var WebSocketPlugin = {
    $webSockets: {},
    $nextWebSocketId: 0,

    WebSocketCreate: function(urlPtr) {
        var url = UTF8ToString(urlPtr);
        var id = nextWebSocketId++;

        var ws = new WebSocket(url);
        webSockets[id] = ws;

        ws.onopen = function() {
            try {
                Module.dynCall_vi(Module._OnWebSocketOpen, id);
            } catch (e) {
                console.log("WebSocket onopen callback error:", e);
            }
        };

        ws.onmessage = function(event) {
            try {
                var msgPtr = allocate(intArrayFromString(event.data), ALLOC_NORMAL);
                Module.dynCall_vii(Module._OnWebSocketMessage, id, msgPtr);
                _free(msgPtr);
            } catch (e) {
                console.log("WebSocket onmessage callback error:", e);
            }
        };

        ws.onclose = function() {
            try {
                Module.dynCall_vi(Module._OnWebSocketClose, id);
            } catch (e) {
                console.log("WebSocket onclose callback error:", e);
            }
        };

        ws.onerror = function(event) {
            try {
                var errorMsg = "WebSocket error";
                var errorPtr = allocate(intArrayFromString(errorMsg), ALLOC_NORMAL);
                Module.dynCall_vii(Module._OnWebSocketError, id, errorPtr);
                _free(errorPtr);
            } catch (e) {
                console.log("WebSocket onerror callback error:", e);
            }
        };

        return id;
    },

    WebSocketSend: function(id, messagePtr) {
        var ws = webSockets[id];
        if (ws && ws.readyState === WebSocket.OPEN) {
            var message = UTF8ToString(messagePtr);
            ws.send(message);
        }
    },

    WebSocketClose: function(id) {
        var ws = webSockets[id];
        if (ws) {
            ws.close();
            delete webSockets[id];
        }
    },

    WebSocketGetState: function(id) {
        var ws = webSockets[id];
        if (!ws) return 3; // CLOSED
        return ws.readyState;
    }
};

autoAddDeps(WebSocketPlugin, '$webSockets');
autoAddDeps(WebSocketPlugin, '$nextWebSocketId');
mergeInto(LibraryManager.library, WebSocketPlugin);
