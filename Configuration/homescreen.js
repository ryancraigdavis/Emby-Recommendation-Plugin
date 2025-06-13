// Home Screen Recommendations Integration
(function() {
    'use strict';

    var RecommendationHomeScreen = {
        
        // Add recommendation rows to home screen
        addRecommendationRows: function() {
            // Wait for home screen to load
            if (typeof ApiClient === 'undefined' || !document.querySelector('.homePage')) {
                setTimeout(RecommendationHomeScreen.addRecommendationRows, 1000);
                return;
            }

            console.log('Adding recommendation rows to home screen');
            
            // Find the home page sections container
            var sectionsContainer = document.querySelector('.sections');
            if (!sectionsContainer) {
                console.log('Home page sections container not found');
                return;
            }

            // Find "Continue Watching" section to insert recommendations after it
            var continueWatchingSection = RecommendationHomeScreen.findContinueWatchingSection(sectionsContainer);
            var insertAfter = continueWatchingSection || sectionsContainer.firstElementChild;

            // Add recommendation sections right after Continue Watching
            insertAfter = RecommendationHomeScreen.addRecommendationSection(sectionsContainer, 'recommended', 'Recommended for You', insertAfter);
            insertAfter = RecommendationHomeScreen.addRecommendationSection(sectionsContainer, 'trending', 'Trending Now', insertAfter);
            insertAfter = RecommendationHomeScreen.addRecommendationSection(sectionsContainer, 'similar', 'More Like Your Favorites', insertAfter);
        },

        findContinueWatchingSection: function(container) {
            // Look for Continue Watching section by various possible selectors
            var selectors = [
                '.homePageSection:has(.sectionTitle:contains("Continue Watching"))',
                '.homePageSection [data-title*="Continue"]',
                '.homePageSection [data-title*="Resume"]',
                '.continueWatching',
                '.resumeSection'
            ];

            for (var i = 0; i < selectors.length; i++) {
                try {
                    var element = container.querySelector(selectors[i]);
                    if (element) {
                        // Make sure we get the section, not a child element
                        while (element && !element.classList.contains('homePageSection')) {
                            element = element.parentElement;
                        }
                        if (element) {
                            console.log('Found Continue Watching section');
                            return element;
                        }
                    }
                } catch (e) {
                    // Continue to next selector if this one fails
                }
            }

            // Fallback: look for section with "Continue" or "Resume" in text content
            var sections = container.querySelectorAll('.homePageSection');
            for (var j = 0; j < sections.length; j++) {
                var sectionText = sections[j].textContent.toLowerCase();
                if (sectionText.includes('continue') || sectionText.includes('resume')) {
                    console.log('Found Continue Watching section by text content');
                    return sections[j];
                }
            }

            console.log('Continue Watching section not found, will insert at beginning');
            return null;
        },

        addRecommendationSection: function(container, type, title, insertAfterElement) {
            // Check if section already exists (avoid duplicates)
            var existingSection = container.querySelector(`[data-recommendation-type="${type}"]`);
            if (existingSection) {
                console.log(`Recommendation section ${type} already exists, skipping`);
                return existingSection;
            }

            // Create section element
            var section = document.createElement('div');
            section.className = 'homePageSection';
            section.setAttribute('data-recommendation-type', type);
            
            var html = `
                <div class="sectionHeader">
                    <h2 class="sectionTitle sectionTitle-cards padded-left">${title}</h2>
                </div>
                <div class="itemsContainer vertical-wrap">
                    <div class="cardLayout" data-recommendation-cards="${type}">
                        <div class="scrollSlider" style="white-space: nowrap; position: relative;">
                            <div class="recommendation-loading" style="text-align: center; padding: 20px;">
                                <div class="loading-spinner"></div>
                                <p>Loading recommendations...</p>
                            </div>
                        </div>
                    </div>
                </div>
            `;
            
            section.innerHTML = html;
            
            // Insert after the specified element
            if (insertAfterElement && insertAfterElement.nextSibling) {
                container.insertBefore(section, insertAfterElement.nextSibling);
            } else if (insertAfterElement) {
                container.appendChild(section);
            } else {
                // Fallback: insert at beginning
                container.insertBefore(section, container.firstChild);
            }

            console.log(`Added recommendation section: ${title}`);

            // Load recommendations for this section
            RecommendationHomeScreen.loadRecommendations(type);
            
            // Return this section so the next one can be inserted after it
            return section;
        },

        loadRecommendations: function(type) {
            var endpoint = `/Plugins/RecommendationPlugin/HomeScreen/${type.charAt(0).toUpperCase() + type.slice(1)}`;
            
            // This endpoint automatically falls back to Emby recommendations if AI service is down
            fetch(endpoint, {
                method: 'GET',
                headers: {
                    'X-Emby-Token': ApiClient.accessToken()
                }
            })
            .then(function(response) {
                if (!response.ok) {
                    throw new Error('Failed to load recommendations');
                }
                return response.json();
            })
            .then(function(result) {
                RecommendationHomeScreen.renderRecommendations(type, result.Items || []);
            })
            .catch(function(error) {
                console.error('Error loading recommendations for', type, error);
                RecommendationHomeScreen.showRecommendationError(type);
            });
        },

        renderRecommendations: function(type, items) {
            var container = document.querySelector(`[data-recommendation-cards="${type}"] .scrollSlider`);
            if (!container) return;

            if (!items || items.length === 0) {
                container.innerHTML = '<div style="text-align: center; padding: 20px; color: #999;">No recommendations available</div>';
                return;
            }

            var cardsHtml = items.map(function(item) {
                return RecommendationHomeScreen.createCardHtml(item);
            }).join('');

            container.innerHTML = cardsHtml;

            // Add click handlers
            container.addEventListener('click', function(e) {
                var card = e.target.closest('[data-item-id]');
                if (card) {
                    var itemId = card.getAttribute('data-item-id');
                    RecommendationHomeScreen.openItem(itemId);
                }
            });
        },

        createCardHtml: function(item) {
            var imageUrl = '';
            if (item.ImageTags && item.ImageTags.Primary) {
                imageUrl = `${ApiClient.serverAddress()}/Items/${item.Id}/Images/Primary?tag=${item.ImageTags.Primary}&quality=90&maxWidth=400`;
            }

            var year = '';
            if (item.ProductionYear) {
                year = `<div class="cardText cardTextCentered">${item.ProductionYear}</div>`;
            }

            return `
                <div class="card backdropCard card-hoverable card-withuserdata" data-item-id="${item.Id}" style="display: inline-block; margin-right: 10px;">
                    <div class="cardBox visualCardBox">
                        <div class="cardScalable">
                            <div class="cardPadder cardPadder-backdrop"></div>
                            <div class="cardContent">
                                <div class="cardImageContainer coveredImage">
                                    ${imageUrl ? `<img class="cardImage cardImage-img" src="${imageUrl}" alt="${item.Name}">` : '<div class="cardImage cardImage-img" style="background-color: #333;"></div>'}
                                </div>
                                <div class="cardOverlayContainer">
                                    <div class="cardOverlayInner">
                                        <div class="cardText cardTextCentered">${item.Name}</div>
                                        ${year}
                                    </div>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            `;
        },

        openItem: function(itemId) {
            if (typeof Dashboard !== 'undefined' && Dashboard.navigate) {
                Dashboard.navigate('itemdetails.html?id=' + itemId);
            } else {
                window.location.href = 'itemdetails.html?id=' + itemId;
            }
        },

        showRecommendationError: function(type) {
            var container = document.querySelector(`[data-recommendation-cards="${type}"] .scrollSlider`);
            if (container) {
                container.innerHTML = '<div style="text-align: center; padding: 20px; color: #999;">Unable to load recommendations</div>';
            }
        },

        // Initialize when DOM is ready
        init: function() {
            if (document.readyState === 'loading') {
                document.addEventListener('DOMContentLoaded', RecommendationHomeScreen.addRecommendationRows);
            } else {
                RecommendationHomeScreen.addRecommendationRows();
            }

            // Also listen for navigation events in case of SPA navigation
            if (typeof Events !== 'undefined') {
                Events.on(window, 'beforenavigate', function(e) {
                    if (e.detail.path && e.detail.path.indexOf('home.html') !== -1) {
                        setTimeout(RecommendationHomeScreen.addRecommendationRows, 500);
                    }
                });
            }
        }
    };

    // Auto-initialize
    RecommendationHomeScreen.init();

})();