window.createChart = (canvasId, config) => {
    // Retry mechanism to wait for DOM element
    const createChartWithRetry = (retryCount = 0) => {
        const canvas = document.getElementById(canvasId);
        if (!canvas) {
            if (retryCount < 10) {
                console.log(`Canvas with id ${canvasId} not found, retrying... (${retryCount + 1}/10)`);
                setTimeout(() => createChartWithRetry(retryCount + 1), 100);
                return;
            } else {
                console.error(`Canvas with id ${canvasId} not found after 10 retries`);
                return;
            }
        }

        // Destroy existing chart if it exists
        if (window.charts && window.charts[canvasId]) {
            window.charts[canvasId].destroy();
        }

        // Initialize charts object if it doesn't exist
        if (!window.charts) {
            window.charts = {};
        }

        // Create new chart
        const ctx = canvas.getContext('2d');
        const chart = new Chart(ctx, config);
        
        // Store chart reference
        window.charts[canvasId] = chart;
        console.log(`Chart created successfully for canvas ${canvasId}`);
    };

    createChartWithRetry();
};

window.destroyChart = (canvasId) => {
    if (window.charts && window.charts[canvasId]) {
        window.charts[canvasId].destroy();
        delete window.charts[canvasId];
    }
};

window.updateChart = (canvasId, config) => {
    if (window.charts && window.charts[canvasId]) {
        window.charts[canvasId].data = config.data;
        window.charts[canvasId].options = config.options;
        window.charts[canvasId].update();
    }
};
