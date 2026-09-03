// wwwroot/js/layoutHelpers.js
// Layout and scroll management for Blazor

window.LayoutHelpers = {
    initializeScrollObserver: function() {
        const mainContent = document.getElementById('mainContentScroller');
        if (!mainContent) return;

        let resizeTimeout;
        const cardHeights = new Map(); // Store heights for each card
        
        // Measure actual footer dimensions
        const footer = document.querySelector('.system-footer');
        const footerHeight = footer ? footer.offsetHeight : 50;
        
        console.log('📐 Footer height:', footerHeight + 'px');
        
        const adjustContentCardHeight = (detailCard, isExpanding) => {
            // Find the parent content-card
            const contentCard = detailCard.closest('.content-card');
            if (!contentCard) return;

            // Get the detail card content
            const cardContent = detailCard.querySelector('.detail-card-content');
            if (!cardContent) return;

            const cardId = detailCard.id || detailCard.getAttribute('data-card-id') || 
                          Array.from(contentCard.querySelectorAll('.detail-card')).indexOf(detailCard);

            if (isExpanding) {
                // Store the collapsed height before expansion
                const collapsedHeight = 36; // Assume default collapsed height
                cardHeights.set(cardId, collapsedHeight);
                
                // Wait for expansion animation to complete
                setTimeout(() => {
                    const expandedHeight = detailCard.offsetHeight;
                    const heightDiff = expandedHeight - collapsedHeight;
                    
                    console.log('📏 Card expansion:', {
                        cardId: cardId,
                        collapsedHeight: collapsedHeight + 'px',
                        expandedHeight: expandedHeight + 'px',
                        difference: heightDiff + 'px'
                    });
                    
                    if (heightDiff > 0) {
                        // Grow the content-card by the difference
                        const currentHeight = contentCard.clientHeight;
                        const newHeight = currentHeight + heightDiff;
                        contentCard.style.height = newHeight + 'px';
                        contentCard.style.minHeight = newHeight + 'px';
                        
                        console.log('✅ Content card grown to:', newHeight + 'px');
                    }
                }, 350); // Match CSS transition time
            } else {
                // Collapsing - shrink back to stored height
                const expandedHeight = detailCard.offsetHeight;
                
                setTimeout(() => {
                    const collapsedHeight = cardHeights.get(cardId) || detailCard.offsetHeight;
                    const heightDiff = expandedHeight - collapsedHeight;
                    
                    console.log('📏 Card collapse:', {
                        cardId: cardId,
                        expandedHeight: expandedHeight + 'px',
                        collapsedHeight: collapsedHeight + 'px',
                        difference: heightDiff + 'px'
                    });
                    
                    if (heightDiff > 0) {
                        // Shrink the content-card by the difference
                        const currentHeight = contentCard.offsetHeight;
                        const newHeight = Math.max(currentHeight - heightDiff, 0);
                        
                        if (newHeight > 0) {
                            contentCard.style.height = newHeight + 'px';
                            contentCard.style.minHeight = newHeight + 'px';
                            
                            console.log('✅ Content card shrunk to:', newHeight + 'px');
                        } else {
                            // Reset to auto if calculation results in 0
                            contentCard.style.height = 'auto';
                            contentCard.style.minHeight = '0';
                        }
                    }
                    
                    cardHeights.delete(cardId);
                }, 350);
            }
        };

        // Watch for DOM mutations (cards expanding/collapsing, content loading)
        const observer = new MutationObserver((mutations) => {
            mutations.forEach(mutation => {
                // Check for class changes on detail-card (collapsed <-> expanded)
                if (mutation.type === 'attributes' && mutation.attributeName === 'class') {
                    const target = mutation.target;
                    if (target.classList && target.classList.contains('detail-card')) {
                        const isExpanded = target.classList.contains('expanded');
                        const wasExpanded = mutation.oldValue && mutation.oldValue.includes('expanded');
                        
                        // Detect expansion or collapse
                        if (isExpanded && !wasExpanded) {
                            console.log('🔽 Card expanding...');
                            adjustContentCardHeight(target, true);
                        } else if (!isExpanded && wasExpanded) {
                            console.log('🔼 Card collapsing...');
                            adjustContentCardHeight(target, false);
                        }
                    }
                }
            });
        });

        // Observe the dynamic content container
        const dynamicContent = document.getElementById('dynamicContent');
        if (dynamicContent) {
            observer.observe(dynamicContent, {
                childList: true,
                subtree: true,
                attributes: true,
                attributeFilter: ['class'],
                attributeOldValue: true
            });

            console.log('✅ Scroll recalculation observer initialized');
        }

        // Handle window resize with debouncing
        window.addEventListener('resize', () => {
            clearTimeout(resizeTimeout);
            resizeTimeout = setTimeout(() => {
                console.log('🔄 Window resized, recalculating card heights...');
                // Could add recalculation logic here if needed
            }, 250);
        });

        console.log('✅ Layout helpers initialized');
    },

    capturePointer: function(el, pointerId) {
        if (el && typeof el.setPointerCapture === "function")
            el.setPointerCapture(pointerId);
    },

    getClientHeight: function(selector) {
        const el = document.querySelector(selector);
        return el ? el.clientHeight : (window.innerHeight || 0);
    }
};
